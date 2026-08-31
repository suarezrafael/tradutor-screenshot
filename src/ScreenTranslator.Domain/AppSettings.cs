namespace ScreenTranslator.Domain;

/// <summary>User-configurable settings, persisted as JSON by the Infrastructure layer.</summary>
public sealed class AppSettings
{
    public string SourceLanguageCode { get; set; } = Language.AutoDetect.Code;
    public string TargetLanguageCode { get; set; } = Language.DefaultTarget.Code;

    public string CaptureHotkey { get; set; } = "Ctrl+Shift+T";
    public string ShowLastResultHotkey { get; set; } = "Ctrl+Shift+L";

    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoCopyTranslation { get; set; }

    /// <summary>Floor for the translation font size - actual size scales up from here proportionally
    /// to each phrase's own original text height (see TranslationOverlayService.FontSizeFor).</summary>
    public double OverlayFontSize { get; set; } = 8.0;
    public double OverlayBackgroundOpacity { get; set; } = 0.75;
    public string OverlayBackgroundColor { get; set; } = "#FFFFFF";
    // Deliberately not black/gray: needs to read as "this is the translation" at a glance, distinct
    // from whatever color the original captured text happens to be.
    public string OverlayTextColor { get; set; } = "#0B5FFF";
}
