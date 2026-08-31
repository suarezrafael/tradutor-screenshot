using System.Windows.Input;

namespace ScreenTranslator.Desktop.Interop;

/// <summary>Parses hotkey strings like "Ctrl+Shift+T" (as stored in <see cref="Domain.AppSettings"/>) into Win32 terms.</summary>
internal static class HotkeyParser
{
    public static bool TryParse(string hotkeyText, out NativeMethods.Modifiers modifiers, out uint virtualKey)
    {
        modifiers = NativeMethods.Modifiers.None;
        virtualKey = 0;

        var parts = hotkeyText.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        Key? mainKey = null;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= NativeMethods.Modifiers.Control;
                    break;
                case "shift":
                    modifiers |= NativeMethods.Modifiers.Shift;
                    break;
                case "alt":
                    modifiers |= NativeMethods.Modifiers.Alt;
                    break;
                case "win":
                case "windows":
                    modifiers |= NativeMethods.Modifiers.Win;
                    break;
                default:
                    if (Enum.TryParse<Key>(part, ignoreCase: true, out var key))
                    {
                        mainKey = key;
                    }
                    break;
            }
        }

        if (mainKey is null)
        {
            return false;
        }

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(mainKey.Value);
        return true;
    }
}
