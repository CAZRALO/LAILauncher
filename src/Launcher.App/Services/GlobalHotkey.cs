using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Launcher.App.Services;

public enum HotkeyRegistrationType
{
    None,
    Native,
    LowLevelHook
}

public class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_MENU = 0x12;       // Alt
    private const int VK_CONTROL = 0x11;    // Ctrl
    private const int VK_SHIFT = 0x10;      // Shift
    private const int VK_LWIN = 0x5B;       // Windows Left
    private const int VK_RWIN = 0x5C;       // Windows Right

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelKeyboardProc? _hookCallbackDelegate;
    private IntPtr _hookId = IntPtr.Zero;

    private readonly int _hotkeyId = 9000;
    private IntPtr _windowHandle;
    private HwndSource? _source;

    public ModifierKeys Modifiers { get; private set; } = ModifierKeys.Alt;
    public Key Key { get; private set; } = Key.Space;
    public bool IsRegistered { get; private set; }
    public HotkeyRegistrationType RegistrationType { get; private set; } = HotkeyRegistrationType.None;

    public event Action? HotkeyPressed;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public bool Register(IntPtr windowHandle, ModifierKeys modifiers = ModifierKeys.Alt, Key key = Key.Space)
    {
        _windowHandle = windowHandle;

        if (_source == null)
        {
            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(HwndHook);
        }

        return UpdateHotkey(modifiers, key);
    }

    public bool UpdateHotkey(ModifierKeys modifiers, Key key)
    {
        if (_windowHandle == IntPtr.Zero) return false;

        // 1. Clean up previous registration
        if (IsRegistered)
        {
            if (RegistrationType == HotkeyRegistrationType.Native)
            {
                UnregisterHotKey(_windowHandle, _hotkeyId);
            }
            else if (RegistrationType == HotkeyRegistrationType.LowLevelHook)
            {
                UninstallHook();
            }
            IsRegistered = false;
            RegistrationType = HotkeyRegistrationType.None;
        }

        Modifiers = modifiers;
        Key = key;

        uint fsModifiers = 0;
        if ((modifiers & ModifierKeys.Alt) != 0) fsModifiers |= MOD_ALT;
        if ((modifiers & ModifierKeys.Control) != 0) fsModifiers |= MOD_CONTROL;
        if ((modifiers & ModifierKeys.Shift) != 0) fsModifiers |= MOD_SHIFT;
        if ((modifiers & ModifierKeys.Windows) != 0) fsModifiers |= MOD_WIN;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        // 2. Try Native RegisterHotKey with MOD_NOREPEAT first
        bool success = RegisterHotKey(_windowHandle, _hotkeyId, fsModifiers | MOD_NOREPEAT, vk);
        if (!success)
        {
            // Fallback attempt without MOD_NOREPEAT (for compatibility)
            success = RegisterHotKey(_windowHandle, _hotkeyId, fsModifiers, vk);
        }

        if (success)
        {
            IsRegistered = true;
            RegistrationType = HotkeyRegistrationType.Native;
            return true;
        }

        // 3. Fall back to Low-Level Keyboard Hook if native registration failed (e.g., error 1409, occupied by PowerToys)
        InstallHook();
        if (_hookId != IntPtr.Zero)
        {
            IsRegistered = true;
            RegistrationType = HotkeyRegistrationType.LowLevelHook;
            return true;
        }

        IsRegistered = false;
        RegistrationType = HotkeyRegistrationType.None;
        return false;
    }

    private void InstallHook()
    {
        if (_hookId != IntPtr.Zero) return;

        _hookCallbackDelegate = LowLevelKeyboardHandler;
        var moduleHandle = GetModuleHandle(null);
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookCallbackDelegate, moduleHandle, 0);
    }

    private void UninstallHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _hookCallbackDelegate = null;
        }
    }

    private IntPtr LowLevelKeyboardHandler(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint vkCode = hookStruct.vkCode;
                uint targetVk = (uint)KeyInterop.VirtualKeyFromKey(Key);

                if (vkCode == targetVk)
                {
                    bool isAltDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                    bool isCtrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    bool isShiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                    bool isWinDown = ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0) || ((GetAsyncKeyState(VK_RWIN) & 0x8000) != 0);

                    bool reqAlt = (Modifiers & ModifierKeys.Alt) != 0;
                    bool reqCtrl = (Modifiers & ModifierKeys.Control) != 0;
                    bool reqShift = (Modifiers & ModifierKeys.Shift) != 0;
                    bool reqWin = (Modifiers & ModifierKeys.Windows) != 0;

                    if (isAltDown == reqAlt && isCtrlDown == reqCtrl && isShiftDown == reqShift && isWinDown == reqWin)
                    {
                        HotkeyPressed?.Invoke();
                        return (IntPtr)1; // Swallow the key event so other conflicting apps don't handle it
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");

        var keyName = key switch
        {
            Key.Space => "Space",
            Key.Return => "Enter",
            _ => key.ToString()
        };
        parts.Add(keyName);

        return string.Join(" + ", parts);
    }

    public static bool TryParseHotkey(string text, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(text)) return false;

        var tokens = text.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawToken in tokens)
        {
            var token = rawToken.Trim().ToLowerInvariant();
            if (token is "ctrl" or "control")
            {
                modifiers |= ModifierKeys.Control;
            }
            else if (token is "alt")
            {
                modifiers |= ModifierKeys.Alt;
            }
            else if (token is "shift")
            {
                modifiers |= ModifierKeys.Shift;
            }
            else if (token is "win" or "windows")
            {
                modifiers |= ModifierKeys.Windows;
            }
            else
            {
                if (token is "space")
                {
                    key = Key.Space;
                }
                else if (Enum.TryParse<Key>(rawToken.Trim(), true, out var parsedKey))
                {
                    key = parsedKey;
                }
            }
        }

        return key != Key.None;
    }

    public void Dispose()
    {
        if (RegistrationType == HotkeyRegistrationType.Native && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, _hotkeyId);
        }
        else if (RegistrationType == HotkeyRegistrationType.LowLevelHook)
        {
            UninstallHook();
        }

        IsRegistered = false;
        RegistrationType = HotkeyRegistrationType.None;

        if (_windowHandle != IntPtr.Zero)
        {
            _source?.RemoveHook(HwndHook);
            _windowHandle = IntPtr.Zero;
        }
    }
}
