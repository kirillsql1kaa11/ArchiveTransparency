using System.Runtime.InteropServices;
using System.Windows.Interop;
using Serilog;
using ArchiveTransparency.Helpers;

namespace ArchiveTransparency.Services;

public class HotkeyService : IDisposable
{
    private readonly ILogger _logger;
    private readonly Dictionary<int, HotkeyEntry> _hotkeys = new();
    private int _nextId = 1;
    private HwndSource? _hwndSource;
    private bool _disposed;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public HotkeyService(ILogger logger)
    {
        _logger = logger;
    }

    public void Initialize(IntPtr hwnd)
    {
        if (_hwndSource != null) return;

        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
        _logger.Information("HotkeyService initialized with HWND {Hwnd}", hwnd);
    }

    public bool RegisterHotkey(ModifierKeys modifiers, Keys key, string name)
    {
        if (_hwndSource == null || _hwndSource.Handle == IntPtr.Zero)
        {
            _logger.Warning("Cannot register hotkey {Name}: HWND not available", name);
            return false;
        }

        int id = _nextId++;
        bool result = NativeMethods.RegisterHotKey(_hwndSource.Handle, id, (uint)modifiers, (uint)key);

        if (result)
        {
            _hotkeys[id] = new HotkeyEntry { Id = id, Modifiers = modifiers, Key = key, Name = name };
            _logger.Information("Hotkey {Name} registered with ID {Id}", name, id);
            return true;
        }

        _logger.Warning("Failed to register hotkey {Name}", name);
        return false;
    }

    public bool RegisterHotkey(string hotkeyString, string name)
    {
        if (TryParseHotkey(hotkeyString, out var modifiers, out var key))
        {
            return RegisterHotkey(modifiers, key, name);
        }
        _logger.Warning("Invalid hotkey string: {Hotkey}", hotkeyString);
        return false;
    }

    public void UnregisterHotkey(string name)
    {
        var entry = _hotkeys.FirstOrDefault(x => x.Value.Name == name);
        if (entry.Key != 0)
        {
            UnregisterHotkey(entry.Key);
        }
    }

    public void UnregisterHotkey(int id)
    {
        if (_hotkeys.Remove(id, out var entry))
        {
            NativeMethods.UnregisterHotKey(_hwndSource?.Handle ?? IntPtr.Zero, id);
            _logger.Information("Hotkey {Name} unregistered", entry.Name);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeys.Keys.ToList())
        {
            UnregisterHotkey(id);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeys.TryGetValue(id, out var entry))
            {
                _logger.Debug("Hotkey pressed: {Name}", entry.Name);
                HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(entry.Name, entry.Modifiers, entry.Key));
            }
            handled = true;
        }

        return IntPtr.Zero;
    }

    public static bool TryParseHotkey(string hotkeyString, out ModifierKeys modifiers, out Keys key)
    {
        modifiers = ModifierKeys.None;
        key = Keys.None;

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts.Take(parts.Length - 1))
        {
            switch (part.Trim().ToLowerInvariant())
            {
                case "ctrl": modifiers |= ModifierKeys.Control; break;
                case "alt": modifiers |= ModifierKeys.Alt; break;
                case "shift": modifiers |= ModifierKeys.Shift; break;
                case "win": modifiers |= ModifierKeys.Win; break;
            }
        }

        var keyPart = parts.Last().Trim();
        if (Enum.TryParse<Keys>(keyPart, true, out key))
        {
            return true;
        }

        // Try single character
        if (keyPart.Length == 1 && char.IsLetter(keyPart[0]))
        {
            key = (Keys)char.ToUpper(keyPart[0]);
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;

        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
        _disposed = true;
    }

    [Flags]
    public enum ModifierKeys : uint
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }

    public enum Keys
    {
        None = 0,
        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A,
        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,
    }

    private class HotkeyEntry
    {
        public int Id { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public Keys Key { get; set; }
        public string Name { get; set; } = "";
    }
}

public class HotkeyPressedEventArgs : EventArgs
{
    public string Name { get; }
    public HotkeyService.ModifierKeys Modifiers { get; }
    public HotkeyService.Keys Key { get; }

    public HotkeyPressedEventArgs(string name, HotkeyService.ModifierKeys modifiers, HotkeyService.Keys key)
    {
        Name = name;
        Modifiers = modifiers;
        Key = key;
    }
}
