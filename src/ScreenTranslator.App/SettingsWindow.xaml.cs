using System.Windows;
using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;
// Window.Language (System.Windows.Markup.XmlLanguage) shadows the bare name "Language" inside a
// Window subclass, so every reference to our Domain type needs this alias instead.
using AppLanguage = ScreenTranslator.Domain.Language;

namespace ScreenTranslator.Desktop;

public partial class SettingsWindow : Window
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly AppSettings _settings;

    public event Action<AppSettings>? SettingsSaved;

    public SettingsWindow(IAppSettingsStore settingsStore, AppSettings currentSettings)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        _settings = currentSettings;

        SourceLanguageCombo.ItemsSource = AppLanguage.SupportedSourceLanguages;
        TargetLanguageCombo.ItemsSource = AppLanguage.SupportedTargetLanguages;

        SourceLanguageCombo.SelectedItem = AppLanguage.FromCode(_settings.SourceLanguageCode) ?? AppLanguage.AutoDetect;
        TargetLanguageCombo.SelectedItem = AppLanguage.FromCode(_settings.TargetLanguageCode) ?? AppLanguage.DefaultTarget;
        CaptureHotkeyBox.Text = _settings.CaptureHotkey;
        LastResultHotkeyBox.Text = _settings.ShowLastResultHotkey;
        FontSizeSlider.Value = _settings.OverlayFontSize;
        OpacitySlider.Value = _settings.OverlayBackgroundOpacity;
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        MinimizeToTrayCheck.IsChecked = _settings.MinimizeToTray;
        AutoCopyCheck.IsChecked = _settings.AutoCopyTranslation;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _settings.SourceLanguageCode = ((AppLanguage)SourceLanguageCombo.SelectedItem).Code;
        _settings.TargetLanguageCode = ((AppLanguage)TargetLanguageCombo.SelectedItem).Code;
        _settings.CaptureHotkey = CaptureHotkeyBox.Text.Trim();
        _settings.ShowLastResultHotkey = LastResultHotkeyBox.Text.Trim();
        _settings.OverlayFontSize = FontSizeSlider.Value;
        _settings.OverlayBackgroundOpacity = OpacitySlider.Value;
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked ?? false;
        _settings.MinimizeToTray = MinimizeToTrayCheck.IsChecked ?? false;
        _settings.AutoCopyTranslation = AutoCopyCheck.IsChecked ?? false;

        _settingsStore.Save(_settings);
        SettingsSaved?.Invoke(_settings);
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
