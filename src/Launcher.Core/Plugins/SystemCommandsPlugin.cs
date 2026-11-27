using System.Diagnostics;
using System.Runtime.InteropServices;
using Launcher.Core.Search;
using Launcher.Core.Utilities;
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
        Action? ExecuteAction,
        Func<Task<bool>>? ExecuteAsyncAction = null,
        Dictionary<string, string>? ExtraMetadata = null
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

            // --- 4. Battery Saver (100% In-App) ---
            new(
                "sys_battery_toggle",
                "Toggle Battery / Energy Saver (Tiết kiệm pin)",
                "Bật / Tắt chế độ tiết kiệm pin trực tiếp ngay trên ứng dụng",
                "🍃",
                ["toggle battery saver", "energy saver", "tiet kiem pin", "bat tat tiet kiem pin", "tiet kiem nang luong", "battery saver", "chuyen che do pin", "battery"],
                () => WindowsControlService.ToggleBatterySaver(),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["RefreshBatteryUI"] = "True"
                }
            ),
            new(
                "sys_battery_saver_on",
                "Enable Battery Saver",
                "Kích hoạt chế độ tiết kiệm pin ngay trên ứng dụng (Power / Energy Saver)",
                "🪫",
                ["battery saver on", "bat tiet kiem pin", "power saver", "enable battery saver", "tiet kiem nang luong"],
                () => WindowsControlService.EnableBatterySaver(),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["RefreshBatteryUI"] = "True",
                    ["ToastSuccess"] = "🍃 Đã BẬT chế độ Tiết kiệm pin!"
                }
            ),
            new(
                "sys_battery_balanced",
                "Disable Battery Saver (Balanced Power Mode)",
                "Tắt tiết kiệm pin và chuyển sang chế độ nguồn Cân bằng ngay trên ứng dụng",
                "🔋",
                ["balanced power", "che do can bang", "tat tiet kiem pin", "disable battery saver", "pin can bang", "nguon dien"],
                () => WindowsControlService.DisableBatterySaver(),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["RefreshBatteryUI"] = "True",
                    ["ToastSuccess"] = "🔋 Đã TẮT Tiết kiệm pin (Chế độ Cân bằng)!"
                }
            ),

            // --- 5. Wi-Fi & Networks (100% In-App) ---
            new(
                "sys_wifi_flyout",
                "Show Available Wi-Fi Networks (Danh sách Wi-Fi)",
                "Hiển thị danh sách mạng Wi-Fi trực tiếp trên ứng dụng (sys wifi list)",
                "📡",
                ["wifi list", "danh sach wifi", "bat danh sach wifi", "show wifi", "available networks", "ket noi wifi", "mang wifi", "tim wifi", "wifi"],
                null,
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["ActionKeyword"] = "sys wifi list"
                }
            ),
            new(
                "sys_wifi_toggle",
                "Toggle Wi-Fi (Bật / Tắt Wi-Fi)",
                "Chuyển đổi Bật / Tắt kết nối Wi-Fi trực tiếp ngay trên ứng dụng",
                "📶",
                ["toggle wifi", "chuyen doi wifi", "bat tat wifi", "wifi on off", "wifi"],
                null,
                async () => { await WindowsControlService.ToggleWifiAsync(); return true; },
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["StayOpen"] = "True"
                }
            ),
            new(
                "sys_wifi_on",
                "Turn On Wi-Fi",
                "Bật card mạng không dây Wi-Fi trực tiếp trên ứng dụng",
                "📶",
                ["turn on wifi", "bat wifi", "enable wifi", "mo wifi"],
                null,
                async () => await WindowsControlService.SetWifiEnabledAsync(true),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["ToastSuccess"] = "📶 Đã BẬT Wi-Fi!"
                }
            ),
            new(
                "sys_wifi_off",
                "Turn Off Wi-Fi",
                "Tắt card mạng không dây Wi-Fi trực tiếp trên ứng dụng",
                "📴",
                ["turn off wifi", "tat wifi", "disable wifi", "ngat wifi"],
                null,
                async () => await WindowsControlService.SetWifiEnabledAsync(false),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["ToastSuccess"] = "📴 Đã TẮT Wi-Fi!"
                }
            ),

            // --- 6. Bluetooth (100% In-App) ---
            new(
                "sys_bluetooth_list",
                "Show Bluetooth Devices List (Danh sách Bluetooth)",
                "Hiển thị danh sách thiết bị Bluetooth & trạng thái kết nối trực tiếp trên ứng dụng (sys bluetooth list)",
                "📶",
                ["bluetooth list", "bt list", "danh sach bluetooth", "thiet bi bluetooth", "tai nghe bluetooth", "chuot bluetooth", "bluetooth"],
                null,
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["ActionKeyword"] = "sys bluetooth list"
                }
            ),
            new(
                "sys_bluetooth_toggle",
                "Toggle Bluetooth (Bật / Tắt Bluetooth)",
                "Bật / Tắt kết nối sóng Bluetooth trực tiếp ngay trên ứng dụng",
                "🔄",
                ["toggle bluetooth", "bat tat bluetooth", "chuyen bluetooth", "chuyen doi bluetooth", "bluetooth on off", "bluetooth"],
                null,
                async () => { await WindowsControlService.ToggleBluetoothAsync(); return true; },
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["StayOpen"] = "True"
                }
            ),
            new(
                "sys_bluetooth_on",
                "Turn On Bluetooth",
                "Bật sóng Bluetooth trực tiếp ngay trên ứng dụng",
                "🔵",
                ["turn on bluetooth", "bat bluetooth", "enable bluetooth", "mo bluetooth"],
                null,
                async () => await WindowsControlService.SetBluetoothEnabledAsync(true),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["ToastSuccess"] = "📶 Đã BẬT Bluetooth!"
                }
            ),
            new(
                "sys_bluetooth_off",
                "Turn Off Bluetooth",
                "Tắt sóng Bluetooth trực tiếp ngay trên ứng dụng",
                "📴",
                ["turn off bluetooth", "tat bluetooth", "disable bluetooth", "ngat bluetooth"],
                null,
                async () => await WindowsControlService.SetBluetoothEnabledAsync(false),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["ToastSuccess"] = "📴 Đã TẮT Bluetooth!"
                }
            ),
            new(
                "sys_bluetooth_add",
                "Add Bluetooth Device (Pair)",
                "Thêm và ghép nối thiết bị Bluetooth mới",
                "➕",
                ["add bluetooth", "them bluetooth", "pair bluetooth", "ghep noi bluetooth", "ket noi bluetooth"],
                () => WindowsControlService.OpenAddBluetoothDevice()
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

        // Direct in-app Wi-Fi list (triggered by `sys wifi list`, `wifi list`, etc.)
        if (TryExtractSubFilter(query, ["wifi list", "wi-fi list", "list wifi", "danh sach wifi", "wifi ls", "ds wifi"], out var wifiFilter))
        {
            return Task.FromResult<IEnumerable<QueryResult>>(BuildWifiListResults(wifiFilter, context.CancellationToken));
        }

        // Direct in-app Bluetooth list (triggered by `sys bluetooth list`, `sys bt list`, `bluetooth list`, etc.)
        if (TryExtractSubFilter(query, ["bluetooth list", "bt list", "list bluetooth", "danh sach bluetooth", "bluetooth ls", "ds bluetooth"], out var btFilter))
        {
            return Task.FromResult<IEnumerable<QueryResult>>(BuildBluetoothListResults(btFilter, context.CancellationToken));
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

            if (matchScore >= 35.0)
            {
                var finalScore = isExplicitKeyword ? matchScore + 150.0 : matchScore + 30.0;
                results.Add(CreateResult(cmd, finalScore));
            }
        }

        return Task.FromResult<IEnumerable<QueryResult>>(results);
    }

    public static bool IsHardwareListQuery(string rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery)) return false;
        var q = rawQuery.Trim();
        if (q.StartsWith("sys ", StringComparison.OrdinalIgnoreCase)) q = q[4..].Trim();
        else if (q.StartsWith("win ", StringComparison.OrdinalIgnoreCase)) q = q[4..].Trim();

        return TryExtractSubFilter(q, ["wifi list", "wi-fi list", "list wifi", "danh sach wifi", "wifi ls", "ds wifi"], out _) ||
               TryExtractSubFilter(q, ["bluetooth list", "bt list", "list bluetooth", "danh sach bluetooth", "bluetooth ls", "ds bluetooth"], out _);
    }

    private static bool TryExtractSubFilter(string query, string[] triggers, out string filter)
    {
        foreach (var t in triggers)
        {
            if (query.Equals(t, StringComparison.OrdinalIgnoreCase))
            {
                filter = string.Empty;
                return true;
            }
            if (query.StartsWith(t + " ", StringComparison.OrdinalIgnoreCase))
            {
                filter = query[(t.Length + 1)..].Trim();
                return true;
            }
        }
        filter = string.Empty;
        return false;
    }

    private static List<QueryResult> BuildWifiListResults(string filter, CancellationToken token)
    {
        var list = new List<QueryResult>();
        bool wifiOn = WindowsControlService.IsWifiEnabled();
        var networks = wifiOn ? WindowsControlService.GetAvailableWifiNetworks(triggerScan: false) : [];
        var connectedSsid = networks.FirstOrDefault(n => n.IsConnected)?.Ssid;

        // 1. Direct In-App Wi-Fi On/Off Toggle Row
        list.Add(new QueryResult
        {
            Id = "sys_wifi_list_toggle",
            Title = wifiOn
                ? $"📶 Wi-Fi: Đang BẬT{(!string.IsNullOrWhiteSpace(connectedSsid) ? $" ({connectedSsid})" : "")} — Nhấn Enter để TẮT"
                : "📴 Wi-Fi: Đang TẮT — Nhấn Enter để BẬT trực tiếp",
            Subtitle = "Bật / Tắt kết nối Wi-Fi ngay trên LAILauncher không cần mở Windows Settings",
            Score = 1200.0,
            Type = ResultType.SystemCommand,
            IconGlyph = wifiOn ? "📶" : "📴",
            SourcePluginId = "system_commands",
            Metadata = new Dictionary<string, string>
            {
                ["StayOpen"] = "True",
                ["RefreshList"] = "True",
                ["ToastSuccess"] = wifiOn ? "📴 Đã gửi lệnh TẮT Wi-Fi" : "📶 Đã gửi lệnh BẬT Wi-Fi"
            },
            Actions =
            [
                new ResultAction
                {
                    Name = wifiOn ? "Tắt Wi-Fi" : "Bật Wi-Fi",
                    Description = "Chuyển đổi trạng thái Wi-Fi trực tiếp trên ứng dụng",
                    IconGlyph = wifiOn ? "📴" : "📶",
                    ShortcutHint = "Enter",
                    ActionAsync = async () =>
                    {
                        await WindowsControlService.ToggleWifiAsync();
                        return true;
                    }
                }
            ]
        });

        // 2. Refresh / Rescan Row
        if (wifiOn)
        {
            list.Add(new QueryResult
            {
                Id = "sys_wifi_list_refresh",
                Title = "🔄 Quét lại các mạng Wi-Fi xung quanh (Refresh)",
                Subtitle = "Cập nhật danh sách điểm phát Wi-Fi khả dụng mới nhất",
                Score = 1150.0,
                Type = ResultType.SystemCommand,
                IconGlyph = "🔄",
                SourcePluginId = "system_commands",
                Metadata = new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["RefreshList"] = "True",
                    ["ToastSuccess"] = "🔄 Đã cập nhật danh sách Wi-Fi xung quanh"
                },
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Quét lại Wi-Fi",
                        IconGlyph = "🔄",
                        ShortcutHint = "Enter",
                        ActionAsync = async () =>
                        {
                            WindowsControlService.TriggerWifiScan();
                            await Task.Delay(900);
                            return true;
                        }
                    }
                ]
            });
        }

        if (!wifiOn)
        {
            list.Add(new QueryResult
            {
                Id = "sys_wifi_list_off_notice",
                Title = "ℹ️ Wi-Fi đang tắt — Không thể hiển thị danh sách mạng",
                Subtitle = "Chọn dòng 'Wi-Fi: Đang TẮT' phía trên và nhấn Enter để bật lại Wi-Fi",
                Score = 1000.0,
                Type = ResultType.SystemCommand,
                IconGlyph = "ℹ️",
                SourcePluginId = "system_commands",
                Metadata = new Dictionary<string, string> { ["StayOpen"] = "True" }
            });
            return list;
        }

        if (networks.Count <= 1)
        {
            WindowsControlService.TriggerWifiScan();
        }

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? networks
            : networks.Where(n => n.Ssid.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        int idx = 0;
        foreach (var net in filtered)
        {
            if (token.IsCancellationRequested) break;

            double itemScore = (net.IsConnected ? 1140.0 : 1100.0) - idx * 3.0;
            idx++;

            string prefixGlyph = net.IsConnected ? "✅" : (net.IsSecured ? "🔒" : "📶");
            string sigText = net.SignalQuality > 0 ? $"Tín hiệu: {net.SignalQuality}% • " : "";
            string savedText = net.IsConnected
                ? "Đang kết nối • Nhấn Enter để Ngắt kết nối"
                : (net.HasProfile ? "Đã lưu • Nhấn Enter để Kết nối ngay" : (!net.IsSecured ? "Mạng mở • Nhấn Enter để Kết nối" : "Chưa lưu • Nhấn Enter để nhập mật khẩu"));

            bool requiresPwd = !net.IsConnected && !net.HasProfile && net.IsSecured;
            var capturedNet = net;

            list.Add(new QueryResult
            {
                Id = $"sys_wifi_net_{net.Ssid}",
                Title = $"{prefixGlyph} {net.Ssid}" + (net.IsConnected ? " (Đang kết nối)" : ""),
                Subtitle = $"{sigText}{net.AuthAlgorithm} • {savedText}",
                Score = itemScore,
                Type = ResultType.SystemCommand,
                IconGlyph = prefixGlyph,
                SourcePluginId = "system_commands",
                ShortcutHint = net.IsConnected ? "Ngắt" : (requiresPwd ? "Mật khẩu" : "Kết nối"),
                Metadata = new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["RefreshList"] = "True",
                    ["WifiSsid"] = net.Ssid,
                    ["WifiConnected"] = net.IsConnected.ToString(),
                    ["WifiHasProfile"] = net.HasProfile.ToString(),
                    ["WifiSecured"] = net.IsSecured.ToString(),
                    ["WifiProfileName"] = net.ProfileName,
                    ["RequiresWifiPassword"] = requiresPwd.ToString(),
                    ["ToastSuccess"] = net.IsConnected ? $"📴 Đã ngắt kết nối {net.Ssid}" : $"📶 Đang kết nối tới {net.Ssid}..."
                },
                Actions =
                [
                    new ResultAction
                    {
                        Name = net.IsConnected ? $"Ngắt kết nối {net.Ssid}" : $"Kết nối tới {net.Ssid}",
                        IconGlyph = prefixGlyph,
                        ShortcutHint = "Enter",
                        ActionAsync = async () =>
                        {
                            if (capturedNet.IsConnected)
                            {
                                var (ok, _) = await Task.Run(() => WindowsControlService.DisconnectWifi());
                                return ok;
                            }
                            var (connOk, _) = await Task.Run(() =>
                                WindowsControlService.ConnectToWifiNetwork(capturedNet.Ssid, null, capturedNet.ProfileName));
                            return connOk;
                        }
                    }
                ]
            });
        }

        if (filtered.Count == 0)
        {
            list.Add(new QueryResult
            {
                Id = "sys_wifi_empty",
                Title = string.IsNullOrWhiteSpace(filter)
                    ? "📡 Chưa tìm thấy mạng Wi-Fi xung quanh — Nhấn Refresh phía trên để quét lại"
                    : $"🔍 Không tìm thấy mạng Wi-Fi khớp với \"{filter}\"",
                Subtitle = "Thử quét lại hoặc kiểm tra bộ thu phát không dây",
                Score = 900.0,
                Type = ResultType.SystemCommand,
                IconGlyph = "📡",
                SourcePluginId = "system_commands",
                Metadata = new Dictionary<string, string> { ["StayOpen"] = "True", ["RefreshList"] = "True" },
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Quét lại Wi-Fi",
                        ActionAsync = async () => { WindowsControlService.TriggerWifiScan(); await Task.Delay(900); return true; }
                    }
                ]
            });
        }

        return list;
    }

    private static List<QueryResult> BuildBluetoothListResults(string filter, CancellationToken token)
    {
        var list = new List<QueryResult>();
        bool btOn = WindowsControlService.IsBluetoothEnabled();
        var devices = btOn ? WindowsControlService.GetBluetoothDevices(forceRefreshBle: false) : [];
        int connectedCount = devices.Count(d => d.IsConnected);

        // 1. Direct In-App Bluetooth On/Off Toggle Row
        list.Add(new QueryResult
        {
            Id = "sys_bt_list_toggle",
            Title = btOn
                ? $"📶 Bluetooth: Đang BẬT ({connectedCount} thiết bị kết nối) — Nhấn Enter để TẮT"
                : "📴 Bluetooth: Đang TẮT — Nhấn Enter để BẬT trực tiếp",
            Subtitle = "Bật / Tắt sóng Bluetooth trực tiếp ngay trên LAILauncher",
            Score = 1200.0,
            Type = ResultType.SystemCommand,
            IconGlyph = btOn ? "📶" : "📴",
            SourcePluginId = "system_commands",
            Metadata = new Dictionary<string, string>
            {
                ["StayOpen"] = "True",
                ["RefreshList"] = "True",
                ["ToastSuccess"] = btOn ? "📴 Đã TẮT Bluetooth" : "📶 Đã BẬT Bluetooth"
            },
            Actions =
            [
                new ResultAction
                {
                    Name = btOn ? "Tắt Bluetooth" : "Bật Bluetooth",
                    IconGlyph = btOn ? "📴" : "📶",
                    ShortcutHint = "Enter",
                    ActionAsync = async () =>
                    {
                        await WindowsControlService.ToggleBluetoothAsync();
                        return true;
                    }
                }
            ]
        });

        // 2. Refresh Bluetooth Devices Row
        if (btOn)
        {
            list.Add(new QueryResult
            {
                Id = "sys_bt_list_refresh",
                Title = "🔄 Làm mới danh sách thiết bị Bluetooth (Refresh)",
                Subtitle = "Quét lại các thiết bị Classic & Bluetooth LE đã ghép nối",
                Score = 1150.0,
                Type = ResultType.SystemCommand,
                IconGlyph = "🔄",
                SourcePluginId = "system_commands",
                Metadata = new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["RefreshList"] = "True",
                    ["ToastSuccess"] = "🔄 Đã làm mới danh sách thiết bị Bluetooth"
                },
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Làm mới Bluetooth",
                        IconGlyph = "🔄",
                        ShortcutHint = "Enter",
                        ActionAsync = async () =>
                        {
                            await WindowsControlService.RefreshBluetoothDevicesAsync();
                            return true;
                        }
                    }
                ]
            });
        }

        if (!btOn)
        {
            list.Add(new QueryResult
            {
                Id = "sys_bt_list_off_notice",
                Title = "ℹ️ Bluetooth đang tắt — Nhấn Enter ở dòng đầu tiên để bật lại",
                Subtitle = "Danh sách thiết bị đã ghép nối sẽ hiển thị sau khi bật Bluetooth",
                Score = 1000.0,
                Type = ResultType.SystemCommand,
                IconGlyph = "ℹ️",
                SourcePluginId = "system_commands",
                Metadata = new Dictionary<string, string> { ["StayOpen"] = "True" }
            });
            return list;
        }

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? devices
            : devices.Where(d => d.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                 d.Category.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        int idx = 0;
        foreach (var dev in filtered)
        {
            if (token.IsCancellationRequested) break;

            double itemScore = (dev.IsConnected ? 1140.0 : 1100.0) - idx * 3.0;
            idx++;

            var capturedDev = dev;
            string statusTag = dev.IsConnected ? "(Đã kết nối)" : "(Đã ghép nối)";
            string actionHint = dev.IsConnected ? "Nhấn Enter để Ngắt kết nối" : "Nhấn Enter để Kết nối ngay";

            list.Add(new QueryResult
            {
                Id = $"sys_bt_dev_{dev.Address}",
                Title = $"{(dev.IsConnected ? "🟢" : "⚪")} {dev.Name} {statusTag}",
                Subtitle = $"{dev.Category} • MAC: {dev.Address} • {actionHint}",
                Score = itemScore,
                Type = ResultType.SystemCommand,
                IconGlyph = dev.IconGlyph,
                SourcePluginId = "system_commands",
                ShortcutHint = dev.IsConnected ? "Ngắt" : "Kết nối",
                Metadata = new Dictionary<string, string>
                {
                    ["StayOpen"] = "True",
                    ["RefreshList"] = "True",
                    ["BluetoothAddress"] = dev.Address,
                    ["BluetoothName"] = dev.Name,
                    ["BluetoothConnected"] = dev.IsConnected.ToString(),
                    ["ToastSuccess"] = dev.IsConnected ? $"⚪ Đã ngắt kết nối {dev.Name}" : $"🟢 Đã kết nối với {dev.Name}"
                },
                Actions =
                [
                    new ResultAction
                    {
                        Name = dev.IsConnected ? $"Ngắt kết nối {dev.Name}" : $"Kết nối {dev.Name}",
                        IconGlyph = dev.IconGlyph,
                        ShortcutHint = "Enter",
                        ActionAsync = async () =>
                        {
                            var (ok, _) = await WindowsControlService.ToggleBluetoothDeviceConnectionAsync(capturedDev);
                            return ok;
                        }
                    }
                ]
            });
        }

        if (filtered.Count == 0)
        {
            list.Add(new QueryResult
            {
                Id = "sys_bt_empty",
                Title = string.IsNullOrWhiteSpace(filter)
                    ? "🔵 Chưa có thiết bị Bluetooth nào được ghép nối"
                    : $"🔍 Không tìm thấy thiết bị Bluetooth khớp với \"{filter}\"",
                Subtitle = "Nhấn Enter để mở trình ghép nối thiết bị Bluetooth mới",
                Score = 900.0,
                Type = ResultType.SystemCommand,
                IconGlyph = "➕",
                SourcePluginId = "system_commands",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Ghép nối thiết bị Bluetooth mới",
                        SyncAction = () => WindowsControlService.OpenAddBluetoothDevice()
                    }
                ]
            });
        }

        return list;
    }

    private static QueryResult CreateResult(SystemCommandItem cmd, double score)
    {
        var metadata = cmd.ExtraMetadata != null
            ? new Dictionary<string, string>(cmd.ExtraMetadata)
            : new Dictionary<string, string>();

        return new QueryResult
        {
            Id = cmd.Id,
            Title = cmd.Title,
            Subtitle = cmd.Subtitle,
            Score = score,
            Type = ResultType.SystemCommand,
            IconGlyph = cmd.IconGlyph,
            SourcePluginId = "system_commands",
            Metadata = metadata,
            Actions =
            [
                new ResultAction
                {
                    Name = $"Execute {cmd.Title}",
                    Description = cmd.Subtitle,
                    IconGlyph = cmd.IconGlyph,
                    ShortcutHint = "Enter",
                    SyncAction = cmd.ExecuteAction,
                    ActionAsync = cmd.ExecuteAsyncAction
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
