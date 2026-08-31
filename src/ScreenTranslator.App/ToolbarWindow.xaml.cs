using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Desktop.Interop;
using ScreenTranslator.Domain;
// Window.Language (System.Windows.Markup.XmlLanguage) shadows the bare name "Language" inside a
// Window subclass, so every reference to our Domain type needs this alias instead.
using AppLanguage = ScreenTranslator.Domain.Language;

namespace ScreenTranslator.Desktop;

public partial class ToolbarWindow : Window
{
    private readonly CaptureFlowController _flowController;
    private readonly IAppSettingsStore _settingsStore;
    private readonly TrayIconService _trayIcon;
    private readonly GlobalHotkeyManager _hotkeyManager;
    private bool _isLoadingLanguages = true;

    public ToolbarWindow(
        CaptureFlowController flowController,
        IAppSettingsStore settingsStore,
        TrayIconService trayIcon,
        GlobalHotkeyManager hotkeyManager)
    {
        InitializeComponent();
        _flowController = flowController;
        _settingsStore = settingsStore;
        _trayIcon = trayIcon;
        _hotkeyManager = hotkeyManager;

        SourceLanguageCombo.ItemsSource = AppLanguage.SupportedSourceLanguages;
        TargetLanguageCombo.ItemsSource = AppLanguage.SupportedTargetLanguages;
        LoadLanguagesFromSettings();
        _isLoadingLanguages = false;

        _flowController.ResultReady += OnResultReady;
        _flowController.TranslationFailed += OnTranslationFailed;
        _flowController.CaptureStarted += OnCaptureStarted;
        _flowController.CaptureCancelled += OnCaptureEnded;
        _flowController.ProcessingStarted += OnProcessingStarted;
        RegisterHotkeys();

        _trayIcon.CaptureRequested += () => Dispatcher.Invoke(_flowController.StartCapture);
        _trayIcon.ShowLastResultRequested += () => Dispatcher.Invoke(_flowController.ShowLastResult);
        _trayIcon.SettingsRequested += () => Dispatcher.Invoke(OpenSettings);
        _trayIcon.ExitRequested += () => Dispatcher.Invoke(() =>
        {
            _allowClose = true;
            System.Windows.Application.Current.Shutdown();
        });

        Closing += OnClosing;
    }

    private bool _allowClose;
    private bool _wasVisibleBeforeCapture;
    private ProcessingWindow? _processingWindow;

    /// <summary>
    /// This toolbar is Topmost so it stays reachable, but that also means it could sit visually on
    /// top of the fullscreen selection overlay. Hiding it for the duration of the selection avoids
    /// that, while remembering whether it was visible so a hotkey/tray-triggered capture (toolbar
    /// already hidden to the tray) doesn't pop the toolbar back open afterwards.
    /// </summary>
    private void OnCaptureStarted()
    {
        _wasVisibleBeforeCapture = IsVisible;
        Dispatcher.Invoke(Hide);
    }

    private void OnCaptureEnded()
    {
        if (_wasVisibleBeforeCapture)
        {
            Dispatcher.Invoke(Show);
        }
    }

    private void OnProcessingStarted() =>
        Dispatcher.Invoke(() =>
        {
            _processingWindow = new ProcessingWindow();
            _processingWindow.Show();
        });

    private void CloseProcessingWindow()
    {
        if (_processingWindow is null)
        {
            return;
        }

        Dispatcher.Invoke(_processingWindow.Close);
        _processingWindow = null;
    }

    private void LoadLanguagesFromSettings()
    {
        SourceLanguageCombo.SelectedItem = AppLanguage.FromCode(_flowController.Settings.SourceLanguageCode) ?? AppLanguage.AutoDetect;
        TargetLanguageCombo.SelectedItem = AppLanguage.FromCode(_flowController.Settings.TargetLanguageCode) ?? AppLanguage.DefaultTarget;
    }

    private void OnResultReady(Application.TranslationSessionResult result)
    {
        CloseProcessingWindow();
        OnCaptureEnded();
        Dispatcher.Invoke(() =>
        {
            CopyTextButton.IsEnabled = true;
            CopyImageButton.IsEnabled = true;

            var resultWindow = new ResultWindow(result, _flowController.Settings);
            resultWindow.RecaptureRequested += _flowController.StartCapture;
            resultWindow.Show();
        });
    }

    private void OnTranslationFailed(string message)
    {
        CloseProcessingWindow();
        OnCaptureEnded();
        Dispatcher.Invoke(() => MessageBox.Show(this, message, "Screen Translator", MessageBoxButton.OK, MessageBoxImage.Warning));
    }

    private void OnCaptureClick(object sender, RoutedEventArgs e) => _flowController.StartCapture();

    private void OnSourceLanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoadingLanguages || SourceLanguageCombo.SelectedItem is not AppLanguage language)
        {
            return;
        }

        _flowController.Settings.SourceLanguageCode = language.Code;
        _settingsStore.Save(_flowController.Settings);
    }

    private void OnTargetLanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoadingLanguages || TargetLanguageCombo.SelectedItem is not AppLanguage language)
        {
            return;
        }

        _flowController.Settings.TargetLanguageCode = language.Code;
        _settingsStore.Save(_flowController.Settings);
    }

    private void OnCopyTextClick(object sender, RoutedEventArgs e)
    {
        if (_flowController.LastResult is not { } result)
        {
            return;
        }

        var text = string.Join(Environment.NewLine, result.Translations.Select(t => t.TranslatedText));
        if (text.Length > 0)
        {
            Clipboard.SetText(text);
        }
    }

    private void OnCopyImageClick(object sender, RoutedEventArgs e)
    {
        if (_flowController.LastResult is not { } result)
        {
            return;
        }

        var bitmap = new BitmapImage();
        using (var stream = new MemoryStream(result.Capture.ImageBytes))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
        }

        Clipboard.SetImage(bitmap);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_settingsStore, _flowController.Settings) { Owner = this };
        settingsWindow.SettingsSaved += _ =>
        {
            _flowController.ReloadSettings();
            _isLoadingLanguages = true;
            LoadLanguagesFromSettings();
            _isLoadingLanguages = false;
            StartupRegistration.Apply(_flowController.Settings.StartWithWindows);
            RegisterHotkeys();
        };
        settingsWindow.ShowDialog();
    }

    private void RegisterHotkeys()
    {
        _hotkeyManager.UnregisterAll();
        _hotkeyManager.Register(_flowController.Settings.CaptureHotkey, () => Dispatcher.Invoke(_flowController.StartCapture));
        _hotkeyManager.Register(_flowController.Settings.ShowLastResultHotkey, () => Dispatcher.Invoke(_flowController.ShowLastResult));
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !_flowController.Settings.MinimizeToTray)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
