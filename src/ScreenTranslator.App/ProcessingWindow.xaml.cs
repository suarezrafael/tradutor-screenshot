using System.Windows;

namespace ScreenTranslator.Desktop;

/// <summary>Lightweight "processing" indicator shown while OCR/translation runs, so the app doesn't
/// appear to freeze between the selection overlay closing and the result window appearing.</summary>
public partial class ProcessingWindow : Window
{
    public ProcessingWindow()
    {
        InitializeComponent();
    }
}
