using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace ScreenTranslator.Desktop.Interop;

/// <summary>
/// Registers global (system-wide) keyboard shortcuts using RegisterHotKey, routed through a hidden
/// message-only window so they work even while the app has no visible/focused window.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private readonly ILogger<GlobalHotkeyManager> _logger;
    private readonly HwndSource _messageWindow;
    private readonly Dictionary<int, Action> _handlers = [];
    private int _nextId = 1;

    public GlobalHotkeyManager(ILogger<GlobalHotkeyManager> logger)
    {
        _logger = logger;

        var parameters = new HwndSourceParameters("ScreenTranslatorHotkeyWindow")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE: message-only window, never visible
        };

        _messageWindow = new HwndSource(parameters);
        _messageWindow.AddHook(WndProc);
    }

    /// <summary>Registers <paramref name="hotkeyText"/> (e.g. "Ctrl+Shift+T"); returns false if parsing/registration fails.</summary>
    public bool Register(string hotkeyText, Action onPressed)
    {
        if (!HotkeyParser.TryParse(hotkeyText, out var modifiers, out var vk))
        {
            _logger.LogWarning("Could not parse hotkey '{Hotkey}'", hotkeyText);
            return false;
        }

        var id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_messageWindow.Handle, id, modifiers, vk))
        {
            _logger.LogWarning("Failed to register hotkey '{Hotkey}' (already in use by another app?)", hotkeyText);
            return false;
        }

        _handlers[id] = onPressed;
        _logger.LogInformation("Registered global hotkey '{Hotkey}'", hotkeyText);
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _handlers.Keys)
        {
            NativeMethods.UnregisterHotKey(_messageWindow.Handle, id);
        }

        _handlers.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _handlers.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _messageWindow.RemoveHook(WndProc);
        _messageWindow.Dispose();
    }
}
