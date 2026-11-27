using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace Launcher.App.Services;

/// <summary>
/// Manages the Windows notification area (system tray) icon and themed WPF context menu
/// without taking a System.Windows.Forms dependency.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int WM_APP = 0x8000;
    private const int WM_TRAYICON = WM_APP + 100;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const uint LR_DEFAULTSIZE = 0x00000040;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractAssociatedIconW(IntPtr hInst, string lpIconPath, ref ushort lpiIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImageW(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string lpString);

    private readonly Action _onToggleLauncher;
    private readonly Action _onOpenSettings;
    private readonly Action _onOpenProcesses;
    private readonly Action _onSwitchLanguage;
    private readonly Action _onExit;

    private readonly HwndSource _hwndSource;
    private readonly IntPtr _hIcon;
    private readonly uint _taskbarCreatedMsg;
    private readonly ContextMenu _contextMenu;

    private readonly MenuItem _openItem;
    private readonly MenuItem _settingsItem;
    private readonly MenuItem _processesItem;
    private readonly MenuItem _langItem;
    private readonly MenuItem _exitItem;

    private bool _isAdded;
    private bool _isVisible;
    private bool _disposed;

    public TrayIconService(
        Action onToggleLauncher,
        Action onOpenSettings,
        Action onOpenProcesses,
        Action onSwitchLanguage,
        Action onExit)
    {
        _onToggleLauncher = onToggleLauncher;
        _onOpenSettings = onOpenSettings;
        _onOpenProcesses = onOpenProcesses;
        _onSwitchLanguage = onSwitchLanguage;
        _onExit = onExit;

        _taskbarCreatedMsg = RegisterWindowMessageW("TaskbarCreated");

        var parameters = new HwndSourceParameters("LAILauncher_TrayMessageWindow")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE
        };
        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        _hIcon = LoadTrayIconHandle();

        _openItem = new MenuItem();
        _openItem.Click += (_, _) => _onToggleLauncher();

        _settingsItem = new MenuItem();
        _settingsItem.Click += (_, _) => _onOpenSettings();

        _processesItem = new MenuItem();
        _processesItem.Click += (_, _) => _onOpenProcesses();

        _langItem = new MenuItem();
        _langItem.Click += (_, _) => _onSwitchLanguage();

        _exitItem = new MenuItem();
        _exitItem.Click += (_, _) => _onExit();

        _contextMenu = new ContextMenu();
        _contextMenu.Items.Add(_openItem);
        _contextMenu.Items.Add(_settingsItem);
        _contextMenu.Items.Add(_processesItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_langItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_exitItem);

        UpdateMenuLocalization();
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(string _) => UpdateMenuLocalization();

    public void UpdateMenuLocalization()
    {
        _openItem.Header = LocalizationService.Get("Str_Tray_Open", "Mở LAILauncher (Alt+Space)");
        _settingsItem.Header = LocalizationService.Get("Str_Tray_Settings", "Cài đặt hệ thống");
        _processesItem.Header = LocalizationService.Get("Str_Tray_Processes", "Trình chạy ngầm");
        _langItem.Header = LocalizationService.Get("Str_Tray_SwitchLang", "Switch to English / Đổi ngôn ngữ");
        _exitItem.Header = LocalizationService.Get("Str_Tray_Exit", "Thoát hoàn toàn");

        if (_isAdded && _isVisible)
        {
            var data = BuildNotifyIconData();
            Shell_NotifyIconW(NIM_MODIFY, ref data);
        }
    }

    public void Show()
    {
        if (_disposed) return;
        _isVisible = true;
        if (_isAdded) return;

        var data = BuildNotifyIconData();
        _isAdded = Shell_NotifyIconW(NIM_ADD, ref data);
    }

    public void Hide()
    {
        if (_disposed) return;
        _isVisible = false;
        if (!_isAdded) return;

        var data = BuildNotifyIconData();
        Shell_NotifyIconW(NIM_DELETE, ref data);
        _isAdded = false;
    }

    private NOTIFYICONDATA BuildNotifyIconData() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwndSource.Handle,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
        uCallbackMessage = WM_TRAYICON,
        hIcon = _hIcon,
        szTip = LocalizationService.Get("Str_Tray_Tooltip", "LAILauncher — Trợ lý tìm kiếm & điều khiển Windows")
    };

    private static IntPtr LoadTrayIconHandle()
    {
        try
        {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(icoPath))
            {
                var hIcon = LoadImageW(IntPtr.Zero, icoPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
                if (hIcon != IntPtr.Zero) return hIcon;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrayIconService] LoadImageW failed: {ex.Message}");
        }

        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                ushort idx = 0;
                var hAssoc = ExtractAssociatedIconW(IntPtr.Zero, exePath, ref idx);
                if (hAssoc != IntPtr.Zero) return hAssoc;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrayIconService] ExtractAssociatedIconW failed: {ex.Message}");
        }

        return IntPtr.Zero;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_taskbarCreatedMsg != 0 && (uint)msg == _taskbarCreatedMsg && _isVisible)
        {
            _isAdded = false;
            Show();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WM_TRAYICON)
        {
            int mouseMsg = (int)(lParam.ToInt64() & 0xFFFF);
            if (mouseMsg == WM_LBUTTONUP || mouseMsg == WM_LBUTTONDBLCLK)
            {
                Application.Current?.Dispatcher.BeginInvoke(_onToggleLauncher);
                handled = true;
            }
            else if (mouseMsg == WM_RBUTTONUP || mouseMsg == WM_CONTEXTMENU)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    SetForegroundWindow(_hwndSource.Handle);
                    _contextMenu.Placement = PlacementMode.MousePoint;
                    _contextMenu.IsOpen = true;
                });
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        LocalizationService.LanguageChanged -= OnLanguageChanged;

        try
        {
            Hide();
        }
        catch { }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
        }

        try
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
        }
        catch { }
    }
}
