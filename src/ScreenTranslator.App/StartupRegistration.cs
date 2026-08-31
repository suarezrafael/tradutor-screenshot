using Microsoft.Win32;

namespace ScreenTranslator.Desktop;

/// <summary>Toggles "start with Windows" via the current user's Run registry key (no admin rights needed).</summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenTranslator";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(ValueName, Environment.ProcessPath ?? System.Windows.Forms.Application.ExecutablePath);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
