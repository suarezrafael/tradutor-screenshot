using Microsoft.Extensions.Logging;
using ScreenTranslator.Application;
using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;

namespace ScreenTranslator.Desktop;

/// <summary>
/// Ties the capture trigger sources (toolbar button, tray menu, global hotkey) to the region-selection
/// UI and the translation pipeline, and keeps track of the most recent result for "Última captura".
/// </summary>
public sealed class CaptureFlowController(
    IScreenCaptureService captureService,
    CaptureTranslationOrchestrator orchestrator,
    IAppSettingsStore settingsStore,
    ILogger<CaptureFlowController> logger)
{
    public AppSettings Settings { get; private set; } = settingsStore.Load();

    public TranslationSessionResult? LastResult { get; private set; }

    public event Action<TranslationSessionResult>? ResultReady;
    public event Action<string>? TranslationFailed;

    /// <summary>Raised right before the fullscreen selection overlay appears, so other topmost windows can hide.</summary>
    public event Action? CaptureStarted;

    /// <summary>Raised when the user cancels the selection (ESC or a too-small drag) instead of completing it.</summary>
    public event Action? CaptureCancelled;

    /// <summary>
    /// Raised right after a region is selected, before the (possibly multi-second) OCR/translation
    /// pipeline runs, so the UI can show a "processing" indicator instead of appearing to freeze.
    /// </summary>
    public event Action? ProcessingStarted;

    public void ReloadSettings() => Settings = settingsStore.Load();

    public void StartCapture()
    {
        CaptureStarted?.Invoke();

        var virtualBounds = captureService.GetVirtualScreenBounds();
        var overlay = new SelectionOverlayWindow(virtualBounds);

        overlay.Cancelled += () =>
        {
            overlay.Close();
            CaptureCancelled?.Invoke();
        };
        overlay.RegionSelected += async region =>
        {
            overlay.Close();
            await RunTranslationAsync(region);
        };

        overlay.Show();
    }

    public void ShowLastResult()
    {
        if (LastResult is { } result)
        {
            ResultReady?.Invoke(result);
        }
    }

    private async Task RunTranslationAsync(BoundingBox region)
    {
        var sourceLanguage = Language.FromCode(Settings.SourceLanguageCode) ?? Language.AutoDetect;
        var targetLanguage = Language.FromCode(Settings.TargetLanguageCode) ?? Language.DefaultTarget;

        // Captured before the "processing" indicator shows (see CaptureAsync's remarks) - otherwise
        // the indicator window itself can end up baked into the screenshot it's supposedly reporting
        // progress on top of.
        var captureResult = await orchestrator.CaptureAsync(region);
        if (!captureResult.Success)
        {
            logger.LogWarning("Capture failed: {ErrorCode} {Message}", captureResult.ErrorCode, captureResult.ErrorMessage);
            TranslationFailed?.Invoke(captureResult.ErrorMessage ?? "Ocorreu um erro inesperado.");
            return;
        }

        ProcessingStarted?.Invoke();

        var result = await orchestrator.TranslateCapturedRegionAsync(
            captureResult.Value!, sourceLanguage, targetLanguage, Settings.OverlayFontSize);

        if (!result.Success)
        {
            logger.LogWarning("Capture/translation failed: {ErrorCode} {Message}", result.ErrorCode, result.ErrorMessage);
            TranslationFailed?.Invoke(result.ErrorMessage ?? "Ocorreu um erro inesperado.");
            return;
        }

        LastResult = result.Value;
        ResultReady?.Invoke(result.Value!);

        if (Settings.AutoCopyTranslation)
        {
            var text = string.Join(Environment.NewLine, result.Value!.Translations.Select(t => t.TranslatedText));
            if (text.Length > 0)
            {
                System.Windows.Clipboard.SetText(text);
            }
        }
    }
}
