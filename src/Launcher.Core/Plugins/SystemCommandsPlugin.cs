using System.Diagnostics;
using System.Runtime.InteropServices;
using Launcher.Core.Search;
using Launcher.SDK;

namespace Launcher.Core.Plugins;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class SystemCommandsPlugin : ISearchProvider
{
    public string ProviderId => "system_commands";
    public string DisplayName => "System Commands";
    public int Priority => 15;
    public string? ActionKeyword => "sys";
    public bool SupportsGlobalSearch => true;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("PowrProf.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    private record SystemCommandItem(
        string Id,
        string Title,
        string Subtitle,
        string IconGlyph,
        string[] Keywords,
        Action ExecuteAction
    );

    private readonly List<SystemCommandItem> _commands;

    public SystemCommandsPlugin()
    {
        _commands =
        [
            new(
                "sys_lock",
                "Lock Screen",
                "Khóa máy tính (Win + L)",
                "🔒",
                ["lock", "khoa", "lock workstation", "screen lock", "lock screen"],
                () => LockWorkStation()
            ),
            new(
                "sys_shutdown",
                "Shut Down",
                "Tắt nguồn máy tính",
                "⏻",
                ["shutdown", "tat may", "power off", "turn off", "tat nguon"],
                () => SafeStartProcess("shutdown.exe", "/s /t 0")
            ),
            new(
                "sys_restart",
                "Restart Computer",
                "Khởi động lại máy tính",
                "🔄",
                ["restart", "reboot", "khoi dong lai", "reset"],
                () => SafeStartProcess("shutdown.exe", "/r /t 0")
            ),
            new(
                "sys_sleep",
                "Sleep",
                "Chuyển sang chế độ ngủ (Sleep mode)",
                "🌙",
                ["sleep", "ngu", "standby", "suspend"],
                () => SetSuspendState(false, true, false)
            ),
            new(
                "sys_hibernate",
                "Hibernate",
                "Ngủ đông máy tính (Hibernate)",
                "💤",
                ["hibernate", "ngu dong"],
                () => SafeStartProcess("shutdown.exe", "/h")
            ),
            new(
                "sys_logoff",
                "Sign Out / Log Off",
                "Đăng xuất khỏi tài khoản người dùng hiện tại",
                "🚪",
                ["log off", "logoff", "sign out", "signout", "dang xuat"],
                () => SafeStartProcess("shutdown.exe", "/l")
            ),
            new(
                "sys_empty_bin",
                "Empty Recycle Bin",
                "Dọn sạch toàn bộ thùng rác",
                "🗑️",
                ["empty recycle bin", "emptybin", "don rac", "clear recycle bin", "trash", "recycle bin empty", "xoa rac"],
                () => SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOSOUND)
            ),
            new(
                "sys_open_bin",
                "Open Recycle Bin",
                "Mở thư mục Thùng rác",
                "🗑️",
                ["recycle bin", "thung rac", "open trash", "open recycle bin"],
                () => SafeStartProcess("explorer.exe", "shell:RecycleBinFolder")
            ),
            new(
                "sys_taskmgr",
                "Task Manager",
                "Mở trình quản lý tác vụ Task Manager",
                "📊",
                ["task manager", "taskmgr", "quan ly tac vu", "process manager"],
                () => SafeStartProcess("taskmgr.exe")
            ),
            new(
                "sys_control_panel",
                "Control Panel",
                "Mở Bảng điều khiển Control Panel",
                "⚙️",
                ["control panel", "bang dieu khien", "control"],
                () => SafeStartProcess("control.exe")
            ),
            new(
                "sys_devmgmt",
                "Device Manager",
                "Mở trình quản lý thiết bị phần cứng",
                "🔌",
                ["device manager", "devmgmt", "thiet bi", "hardware"],
                () => SafeStartProcess("devmgmt.msc")
            ),
            new(
                "sys_ncpa",
                "Network Connections",
                "Mở danh sách kết nối mạng (Ethernet, Wi-Fi)",
                "🌐",
                ["network connections", "ncpa.cpl", "mang", "wifi", "ethernet", "ip"],
                () => SafeStartProcess("ncpa.cpl")
            ),
            new(
                "sys_services",
                "Windows Services",
                "Quản lý các dịch vụ hệ thống Windows Services",
                "🛠️",
                ["services", "services.msc", "dich vu"],
                () => SafeStartProcess("services.msc")
            ),
            new(
                "sys_sndvol",
                "Volume Mixer",
                "Điều chỉnh âm lượng từng ứng dụng",
                "🔊",
                ["volume mixer", "volume", "am luong", "sound mixer", "audio"],
                () => SafeStartProcess("sndvol.exe")
            ),
            new(
                "sys_diskmgmt",
                "Disk Management",
                "Quản lý phân vùng ổ đĩa",
                "💾",
                ["disk management", "diskmgmt.msc", "o dia", "partition"],
                () => SafeStartProcess("diskmgmt.msc")
            ),
            new(
                "sys_sysdm",
                "System Properties",
                "Xem thông tin hệ thống và cấu hình nâng cao",
                "💻",
                ["system properties", "sysdm.cpl", "thong tin he thong", "about pc"],
                () => SafeStartProcess("sysdm.cpl")
            ),

            // --- 1. Windows Dark / Light Mode ---
            new(
                "sys_darkmode_toggle",
                "Toggle Windows Dark / Light Mode",
                "Bật / Tắt chế độ giao diện Sáng - Tối của Windows",
                "🌓",
                ["dark mode", "light mode", "che do toi", "che do sang", "toggle dark mode", "windows dark mode", "bat dark mode", "tat dark mode", "darkmode", "giao dien toi", "dark theme"],
                () => Launcher.Core.Utilities.WindowsControlService.ToggleWindowsDarkMode()
            ),
            new(
                "sys_darkmode_on",
                "Windows Dark Mode",
                "Bật chế độ giao diện Tối (Dark Theme) cho Windows",
                "🌙",
                ["dark mode on", "bat dark mode", "che do toi windows", "enable dark mode", "darkmode on"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWindowsDarkMode(true)
            ),
            new(
                "sys_lightmode_on",
                "Windows Light Mode",
                "Bật chế độ giao diện Sáng (Light Theme) cho Windows",
                "☀️",
                ["light mode on", "bat light mode", "che do sang windows", "enable light mode", "lightmode on"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWindowsDarkMode(false)
            ),

            // --- 2. Volume Controls ---
            new(
                "sys_volume_up",
                "Volume Up (+5%)",
                "Tăng âm lượng hệ thống (+5%)",
                "🔊",
                ["volume up", "tang am luong", "tang volume", "tang am", "sound up", "louder", "am thanh tang", "tang loa"],
                () => Launcher.Core.Utilities.WindowsControlService.VolumeUp(3)
            ),
            new(
                "sys_volume_down",
                "Volume Down (-5%)",
                "Giảm âm lượng hệ thống (-5%)",
                "🔉",
                ["volume down", "giam am luong", "giam volume", "giam am", "sound down", "quieter", "am thanh giam", "giam loa"],
                () => Launcher.Core.Utilities.WindowsControlService.VolumeDown(3)
            ),
            new(
                "sys_volume_mute",
                "Toggle Mute / Unmute",
                "Bật / Tắt tiếng âm thanh hệ thống (Mute)",
                "🔇",
                ["mute", "unmute", "tat am", "bat am", "tat tieng", "im lang", "silent", "audio mute", "tat loa"],
                () => Launcher.Core.Utilities.WindowsControlService.ToggleMute()
            ),
            new(
                "sys_volume_50",
                "Set Volume 50%",
                "Đặt âm lượng ở mức trung bình 50%",
                "🔉",
                ["volume 50", "am luong 50", "set volume 50", "volume 50%", "am 50"],
                () => Launcher.Core.Utilities.WindowsControlService.SetMasterVolume(50)
            ),
            new(
                "sys_volume_100",
                "Set Volume 100% (Max)",
                "Đặt âm lượng mức tối đa 100%",
                "🔊",
                ["volume 100", "max volume", "am luong toi da", "am luong 100", "volume max", "am 100"],
                () => Launcher.Core.Utilities.WindowsControlService.SetMasterVolume(100)
            ),
            new(
                "sys_volume_20",
                "Set Volume 20% (Low)",
                "Đặt âm lượng mức nhỏ 20%",
                "🔈",
                ["volume 20", "am luong 20", "low volume", "volume nho", "am 20"],
                () => Launcher.Core.Utilities.WindowsControlService.SetMasterVolume(20)
            ),

            // --- 3. Screen Brightness Controls ---
            new(
                "sys_brightness_up",
                "Brightness Up (+10%)",
                "Tăng độ sáng màn hình (+10%)",
                "🔆",
                ["brightness up", "tang do sang", "tang man hinh sang", "tang do sat", "screen brightness up", "sang hon", "tang brightness"],
                () => Launcher.Core.Utilities.WindowsControlService.BrightnessUp(10)
            ),
            new(
                "sys_brightness_down",
                "Brightness Down (-10%)",
                "Giảm độ sáng màn hình (-10%)",
                "🔅",
                ["brightness down", "giam do sang", "giam man hinh sang", "giam do sat", "screen brightness down", "toi hon", "giam brightness"],
                () => Launcher.Core.Utilities.WindowsControlService.BrightnessDown(10)
            ),
            new(
                "sys_brightness_100",
                "Set Brightness 100% (Max)",
                "Đặt độ sáng màn hình mức tối đa 100%",
                "☀️",
                ["brightness 100", "do sang 100", "max brightness", "do sat 100", "do sang toi da"],
                () => Launcher.Core.Utilities.WindowsControlService.SetBrightness(100)
            ),
            new(
                "sys_brightness_50",
                "Set Brightness 50%",
                "Đặt độ sáng màn hình ở mức vừa 50%",
                "🌤️",
                ["brightness 50", "do sang 50", "do sat 50", "brightness medium", "do sang 50%"],
                () => Launcher.Core.Utilities.WindowsControlService.SetBrightness(50)
            ),
            new(
                "sys_brightness_25",
                "Set Brightness 25% (Night)",
                "Đặt độ sáng màn hình mức dịu mắt ban đêm 25%",
                "🌙",
                ["brightness 25", "do sang 25", "do sat 25", "night brightness", "do sang dem", "diu mat"],
                () => Launcher.Core.Utilities.WindowsControlService.SetBrightness(25)
            ),

            // --- 4. Battery Saver ---
            new(
                "sys_battery_toggle",
                "Toggle Energy Saver (Tiết kiệm pin)",
                "Bật / Tắt chế độ tiết kiệm pin trực tiếp không cần mở Settings",
                "🍃",
                ["toggle battery saver", "energy saver", "tiet kiem pin", "bat tat tiet kiem pin", "tiet kiem nang luong", "battery saver", "chuyen che do pin"],
                () => Launcher.Core.Utilities.WindowsControlService.ToggleBatterySaver()
            ),
            new(
                "sys_battery_saver_on",
                "Enable Battery Saver",
                "Kích hoạt chế độ tiết kiệm pin (Power Saver)",
                "🪫",
                ["battery saver on", "bat tiet kiem pin", "power saver", "pin", "tiet kiem nang luong"],
                () => Launcher.Core.Utilities.WindowsControlService.EnableBatterySaver()
            ),
            new(
                "sys_battery_balanced",
                "Balanced Power Mode",
                "Chuyển sang chế độ nguồn điện Cân bằng (Balanced Mode)",
                "🔋",
                ["balanced power", "che do can bang", "tat tiet kiem pin", "pin can bang", "nguon dien"],
                () => Launcher.Core.Utilities.WindowsControlService.DisableBatterySaver()
            ),
            new(
                "sys_battery_settings",
                "Battery & Power Settings",
                "Mở trang cài đặt Nguồn & Tiết kiệm pin Windows",
                "⚡",
                ["battery settings", "cai dat pin", "powersleep", "power settings", "pin"],
                () => Launcher.Core.Utilities.WindowsControlService.OpenBatterySaverSettings()
            ),

            // --- 5. Wi-Fi & Networks ---
            new(
                "sys_wifi_flyout",
                "Show Available Wi-Fi Networks",
                "Bật danh sách các mạng Wi-Fi xung quanh (Network Flyout)",
                "📡",
                ["wifi", "danh sach wifi", "bat danh sach wifi", "show wifi", "available networks", "ket noi wifi", "mang wifi", "wi-fi", "tim wifi"],
                () => Launcher.Core.Utilities.WindowsControlService.ShowAvailableNetworks()
            ),
            new(
                "sys_wifi_on",
                "Turn On Wi-Fi",
                "Bật card mạng không dây Wi-Fi",
                "📶",
                ["turn on wifi", "bat wifi", "enable wifi", "mo wifi"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWifiEnabled(true)
            ),
            new(
                "sys_wifi_off",
                "Turn Off Wi-Fi",
                "Tắt card mạng không dây Wi-Fi",
                "📴",
                ["turn off wifi", "tat wifi", "disable wifi", "ngat wifi"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWifiEnabled(false)
            ),

            // --- 6. Bluetooth ---
            new(
                "sys_bluetooth_settings",
                "Bluetooth & Devices Settings",
                "Mở danh sách kết nối thiết bị Bluetooth & Thiết bị ngoại vi",
                "📶",
                ["bluetooth", "cai dat bluetooth", "bluetooth settings", "thiet bi bluetooth", "tai nghe bluetooth", "chuot bluetooth"],
                () => Launcher.Core.Utilities.WindowsControlService.OpenBluetoothSettings()
            ),
            new(
                "sys_bluetooth_add",
                "Add Bluetooth Device",
                "Thêm và quét thiết bị Bluetooth mới (Pair device)",
                "➕",
                ["add bluetooth", "them bluetooth", "pair bluetooth", "ghep noi bluetooth", "ket noi bluetooth"],
                () => Launcher.Core.Utilities.WindowsControlService.OpenAddBluetoothDevice()
            ),
            new(
                "sys_bluetooth_toggle",
                "Toggle Bluetooth",
                "Bật / Tắt kết nối sóng Bluetooth",
                "🔄",
                ["toggle bluetooth", "bat bluetooth", "tat bluetooth", "chuyen bluetooth", "bluetooth on off"],
                () => Launcher.Core.Utilities.WindowsControlService.ToggleBluetooth()
            ),

            // --- 7. Mobile Hotspot ---
            new(
                "sys_hotspot_settings",
                "Mobile Hotspot Settings",
                "Mở cài đặt cấu hình phát Wi-Fi di động từ máy tính (Hotspot)",
                "📱",
                ["mobile hotspot", "hotspot", "phat wifi", "phat mang", "chia se mang", "tethering", "bat hotspot", "cai dat hotspot"],
                () => Launcher.Core.Utilities.WindowsControlService.OpenMobileHotspotSettings()
            ),
            new(
                "sys_hotspot_toggle",
                "Toggle Mobile Hotspot",
                "Bật / Tắt trạm phát sóng Wi-Fi di động trực tiếp không cần mở Settings",
                "📡",
                ["toggle hotspot", "mobile hotspot", "hotspot", "bat mobile hotspot", "tat mobile hotspot", "bat hotspot", "tat hotspot", "share internet", "phat mang tu may tinh", "phat wifi", "diem phat song"],
                () => Launcher.Core.Utilities.WindowsControlService.ToggleMobileHotspot()
            ),

            // --- 8. Taskbar Position & Alignment ---
            new(
                "sys_taskbar_align_toggle",
                "Toggle Taskbar Alignment (Trái / Giữa)",
                "Chuyển đổi vị trí căn lề thanh Taskbar giữa Căn Giữa và Căn Trái",
                "↔️",
                ["taskbar", "thay doi vi tri taskbar", "doi vi tri taskbar", "vi tri taskbar", "taskbar position", "toggle taskbar", "can le taskbar", "chuyen taskbar", "taskbar align"],
                () => Launcher.Core.Utilities.WindowsControlService.ToggleTaskbarAlignment()
            ),
            new(
                "sys_taskbar_bottom",
                "Taskbar Position: Bottom (Mặc định)",
                "Đặt vị trí thanh Taskbar ở cạnh Dưới màn hình",
                "📌",
                ["taskbar bottom", "taskbar duoi", "vi tri thanh taskbar", "thanh taskbar", "taskbar position bottom", "taskbar xuong duoi"],
                () => Launcher.Core.Utilities.WindowsControlService.SetTaskbarPosition(Launcher.Core.Utilities.TaskbarPosition.Bottom)
            ),
            new(
                "sys_taskbar_top",
                "Taskbar Position: Top",
                "Đặt vị trí thanh Taskbar ở cạnh Trên màn hình",
                "📌",
                ["taskbar top", "taskbar tren", "vi tri thanh taskbar tren", "taskbar position top", "taskbar len tren"],
                () => Launcher.Core.Utilities.WindowsControlService.SetTaskbarPosition(Launcher.Core.Utilities.TaskbarPosition.Top)
            ),
            new(
                "sys_taskbar_left",
                "Taskbar Position: Left",
                "Đặt vị trí thanh Taskbar ở cạnh Trái màn hình",
                "📌",
                ["taskbar left", "taskbar trai", "vi tri thanh taskbar trai", "taskbar position left", "taskbar sang trai"],
                () => Launcher.Core.Utilities.WindowsControlService.SetTaskbarPosition(Launcher.Core.Utilities.TaskbarPosition.Left)
            ),
            new(
                "sys_taskbar_right",
                "Taskbar Position: Right",
                "Đặt vị trí thanh Taskbar ở cạnh Phải màn hình",
                "📌",
                ["taskbar right", "taskbar phai", "vi tri thanh taskbar phai", "taskbar position right", "taskbar sang phai"],
                () => Launcher.Core.Utilities.WindowsControlService.SetTaskbarPosition(Launcher.Core.Utilities.TaskbarPosition.Right)
            ),
            new(
                "sys_taskbar_align_center",
                "Taskbar Align: Center (Windows 11)",
                "Căn giữa các biểu tượng ứng dụng trên thanh Taskbar",
                "🎯",
                ["taskbar align center", "can giua taskbar", "taskbar center", "taskbar icon center", "can giua icon"],
                () => Launcher.Core.Utilities.WindowsControlService.SetTaskbarAlignment(Launcher.Core.Utilities.TaskbarAlignment.Center)
            ),
            new(
                "sys_taskbar_align_left",
                "Taskbar Align: Left (Windows 11/10)",
                "Căn lề trái các biểu tượng ứng dụng trên thanh Taskbar",
                "◀️",
                ["taskbar align left", "can le trai taskbar", "taskbar left align", "taskbar icon left", "can trai icon"],
                () => Launcher.Core.Utilities.WindowsControlService.SetTaskbarAlignment(Launcher.Core.Utilities.TaskbarAlignment.Left)
            ),

            // --- 9. Screen Capture / Screenshot ---
            new(
                "sys_screenshot_full",
                "Chụp toàn màn hình (Full Screen)",
                "Chụp ảnh toàn bộ màn hình và sao chép vào Clipboard (lưu vào Pictures\\Screenshots)",
                "📸",
                ["chup man hinh", "screenshot", "screen capture", "chup anh man hinh", "chup anh", "prtscn", "print screen", "capture screen"],
                () => Launcher.Core.Utilities.WindowsControlService.CaptureAndSaveFullScreen()
            ),
            new(
                "sys_screenshot_snip",
                "Cắt ảnh màn hình (Snipping Tool)",
                "Mở công cụ khoanh vùng chụp ảnh màn hình Windows Snipping Tool (Win+Shift+S)",
                "✂️",
                ["snip", "snipping tool", "cat man hinh", "khoanh vung man hinh", "screen clip", "snipping"],
                () => Launcher.Core.Utilities.WindowsControlService.LaunchScreenSnipping()
            ),

            // --- 9. Windows Accent / Theme Color ---
            new(
                "sys_accent_blue",
                "Windows Accent Color: Electric Blue",
                "Đổi màu theme Windows sang Xanh dương điện tử (#0078D7)",
                "🔵",
                ["theme color blue", "mau theme window xanh", "accent color blue", "doi mau window", "theme window xanh", "thay doi mau theme window"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWindowsAccentColor("#0078D7")
            ),
            new(
                "sys_accent_purple",
                "Windows Accent Color: Royal Purple",
                "Đổi màu theme Windows sang Tím hoàng gia (#744DA9)",
                "🟣",
                ["theme color purple", "mau theme window tim", "accent color purple", "doi mau window tim", "theme window tim"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWindowsAccentColor("#744DA9")
            ),
            new(
                "sys_accent_red",
                "Windows Accent Color: Crimson Red",
                "Đổi màu theme Windows sang Đỏ rực rỡ (#E81123)",
                "🔴",
                ["theme color red", "mau theme window do", "accent color red", "doi mau window do", "theme window do"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWindowsAccentColor("#E81123")
            ),
            new(
                "sys_accent_green",
                "Windows Accent Color: Emerald Green",
                "Đổi màu theme Windows sang Xanh lục bảo (#107C41)",
                "🟢",
                ["theme color green", "mau theme window xanh la", "accent color green", "doi mau window xanh la", "theme window xanh la"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWindowsAccentColor("#107C41")
            ),
            new(
                "sys_accent_orange",
                "Windows Accent Color: Amber Orange",
                "Đổi màu theme Windows sang Cam hổ phách (#CA5010)",
                "🟠",
                ["theme color orange", "mau theme window cam", "accent color orange", "doi mau window cam", "theme window cam"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWindowsAccentColor("#CA5010")
            ),
            new(
                "sys_accent_teal",
                "Windows Accent Color: Vivid Turquoise",
                "Đổi màu theme Windows sang Xanh ngọc lam (#008272)",
                "💎",
                ["theme color teal", "mau theme window ngoc", "accent color teal", "doi mau window ngoc", "theme window ngoc"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWindowsAccentColor("#008272")
            ),
            new(
                "sys_accent_pink",
                "Windows Accent Color: Magenta Pink",
                "Đổi màu theme Windows sang Hồng cánh sen (#E3008C)",
                "🌸",
                ["theme color pink", "mau theme window hong", "accent color pink", "doi mau window hong", "theme window hong"],
                () => Launcher.Core.Utilities.WindowsControlService.SetWindowsAccentColor("#E3008C")
            ),
            new(
                "sys_accent_settings",
                "Open Windows Colors & Accent Settings",
                "Mở bảng tùy chỉnh màu sắc cá nhân hóa của hệ thống Windows",
                "🎨",
                ["theme color", "mau theme window", "thay doi mau theme window", "cai dat mau window", "personalization colors", "accent color"],
                () => Launcher.Core.Utilities.WindowsControlService.OpenPersonalizationColors()
            )
        ];
    }

    public Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context)
    {
        var raw = context.RawQuery.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return Task.FromResult<IEnumerable<QueryResult>>([]);

        var query = raw;
        var isExplicitKeyword = false;

        if (query.StartsWith("sys ", StringComparison.OrdinalIgnoreCase))
        {
            isExplicitKeyword = true;
            query = query[4..].Trim();
        }
        else if (query.Equals("sys", StringComparison.OrdinalIgnoreCase))
        {
            isExplicitKeyword = true;
            query = string.Empty;
        }
        else if (query.StartsWith("win ", StringComparison.OrdinalIgnoreCase))
        {
            isExplicitKeyword = true;
            query = query[4..].Trim();
        }
        else if (query.Equals("win", StringComparison.OrdinalIgnoreCase))
        {
            isExplicitKeyword = true;
            query = string.Empty;
        }

        var results = new List<QueryResult>();

        foreach (var cmd in _commands)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            if (isExplicitKeyword && string.IsNullOrEmpty(query))
            {
                // Show all system commands when user just types 'sys'
                results.Add(CreateResult(cmd, 100.0));
                continue;
            }

            var titleScore = FuzzyMatcher.Score(cmd.Title, query);
            var bestKeywordScore = 0.0;
            foreach (var kw in cmd.Keywords)
            {
                var s = FuzzyMatcher.Score(kw, query);
                if (s > bestKeywordScore) bestKeywordScore = s;
            }

            var matchScore = Math.Max(titleScore, bestKeywordScore);
            if (isExplicitKeyword)
            {
                matchScore = Math.Max(matchScore, 50.0);
            }

            if (matchScore >= 35.0)
            {
                var finalScore = isExplicitKeyword ? matchScore + 150.0 : matchScore + 30.0;
                results.Add(CreateResult(cmd, finalScore));
            }
        }

        return Task.FromResult<IEnumerable<QueryResult>>(results);
    }

    private static QueryResult CreateResult(SystemCommandItem cmd, double score)
    {
        return new QueryResult
        {
            Id = cmd.Id,
            Title = cmd.Title,
            Subtitle = cmd.Subtitle,
            Score = score,
            Type = ResultType.SystemCommand,
            IconGlyph = cmd.IconGlyph,
            SourcePluginId = "system_commands",
            Actions =
            [
                new ResultAction
                {
                    Name = $"Execute {cmd.Title}",
                    Description = cmd.Subtitle,
                    IconGlyph = cmd.IconGlyph,
                    ShortcutHint = "Enter",
                    SyncAction = cmd.ExecuteAction
                }
            ]
        };
    }

    private static void SafeStartProcess(string fileName, string? args = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args ?? string.Empty,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SystemCommandsPlugin] Failed to run {fileName}: {ex.Message}");
        }
    }
}
