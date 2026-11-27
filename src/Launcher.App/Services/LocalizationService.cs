using System.Windows;

namespace Launcher.App.Services;

/// <summary>
/// Manages dynamic runtime localization between Vietnamese ("vi") and English ("en")
/// across all windows and converters.
/// </summary>
public static class LocalizationService
{
    public const string Vietnamese = "vi";
    public const string English = "en";

    private static ResourceDictionary? _activeLanguageDictionary;

    public static string CurrentLanguage { get; private set; } = Vietnamese;

    public static event Action<string>? LanguageChanged;

    private static readonly Dictionary<string, string> FallbackVi = new(StringComparer.Ordinal)
    {
        ["Str_AppName"] = "LAILauncher",
        ["Str_Version"] = "Phiên bản 2.4.0",
        ["Str_SettingsTitle"] = "Cài đặt - LAILauncher",
        ["Str_SearchPlaceholder"] = "Tìm ứng dụng, tệp tin, lệnh (>) hoặc tính toán...",
        ["Str_ResultsHeader"] = "KẾT QUẢ TÌM KIẾM & HÀNH ĐỘNG",
        ["Str_ResultsNavHint"] = "Sử dụng phím mũi tên ↑ / ↓ để chọn",
        ["Str_NoResultsTitle"] = "Không tìm thấy kết quả phù hợp",
        ["Str_NoResultsSub"] = "Thử tìm kiếm với từ khóa khác hoặc sử dụng các cú pháp hỗ trợ:",
        ["Str_StatusReady"] = "Sẵn sàng",
        ["Str_Footer_Open"] = "Mở",
        ["Str_Footer_Admin"] = "Admin",
        ["Str_Footer_Folder"] = "Thư mục",
        ["Str_Footer_Actions"] = "Tác vụ",
        ["Str_Footer_Hide"] = "Ẩn",
        ["Str_Quick_Hotspot"] = "Hotspot",
        ["Str_Quick_Battery"] = "Tiết kiệm pin",
        ["Str_Quick_BatteryOn"] = "Tiết kiệm pin: Bật",
        ["Str_Quick_TaskbarCenter"] = "Taskbar: Giữa",
        ["Str_Quick_TaskbarLeft"] = "Taskbar: Trái",
        ["Str_Quick_Screenshot"] = "Chụp màn hình",
        ["Str_Quick_Processes"] = "Trình chạy ngầm",
        ["Str_Quick_Wifi"] = "Wi-Fi",
        ["Str_Quick_WifiOn"] = "Wi-Fi: Bật",
        ["Str_Quick_WifiOff"] = "Wi-Fi: Tắt",
        ["Str_Quick_Bluetooth"] = "Bluetooth",
        ["Str_Quick_BluetoothOn"] = "Bluetooth: Bật",
        ["Str_Quick_BluetoothOff"] = "Bluetooth: Tắt",
        ["Str_Type_App"] = "Ứng dụng",
        ["Str_Type_Calc"] = "Tính toán",
        ["Str_Type_AI"] = "Trợ lý AI",
        ["Str_Type_Command"] = "Lệnh Shell",
        ["Str_Type_Url"] = "Trang web",
        ["Str_Type_Bookmark"] = "Dấu trang",
        ["Str_Type_Process"] = "Tiến trình",
        ["Str_Type_Setting"] = "Cài đặt",
        ["Str_Type_Folder"] = "Thư mục",
        ["Str_Type_File"] = "Tệp tin",
        ["Str_Type_Clipboard"] = "Khay nhớ tạm",
        ["Str_Type_System"] = "Hệ thống",
        ["Str_Type_Plugin"] = "Tiện ích",
        ["Str_Nav_General"] = "Chung",
        ["Str_Nav_Appearance"] = "Giao diện",
        ["Str_Nav_Window"] = "Cửa sổ & Màn hình",
        ["Str_Nav_Apps"] = "Ứng dụng & Chạy ngầm",
        ["Str_Nav_System"] = "Hệ thống Windows",
        ["Str_Nav_AI"] = "AI cục bộ",
        ["Str_Nav_About"] = "Giới thiệu (About)",
        ["Str_Tray_Tooltip"] = "LAILauncher — Trợ lý tìm kiếm & điều khiển Windows",
        ["Str_Tray_Open"] = "Mở LAILauncher (Alt+Space)",
        ["Str_Tray_Settings"] = "Cài đặt hệ thống",
        ["Str_Tray_Processes"] = "Trình chạy ngầm",
        ["Str_Tray_SwitchLang"] = "Switch to English (EN)",
        ["Str_Tray_Exit"] = "Thoát hoàn toàn"
    };

    private static readonly Dictionary<string, string> FallbackEn = new(StringComparer.Ordinal)
    {
        ["Str_AppName"] = "LAILauncher",
        ["Str_Version"] = "Version 2.4.0",
        ["Str_SettingsTitle"] = "LAILauncher Settings",
        ["Str_SearchPlaceholder"] = "Search apps, files, commands (>) or math...",
        ["Str_ResultsHeader"] = "SEARCH RESULTS & ACTIONS",
        ["Str_ResultsNavHint"] = "Use ↑ / ↓ arrow keys to navigate",
        ["Str_NoResultsTitle"] = "No matching results found",
        ["Str_NoResultsSub"] = "Try another keyword or use a quick prefix syntax:",
        ["Str_StatusReady"] = "Ready",
        ["Str_Footer_Open"] = "Open",
        ["Str_Footer_Admin"] = "Admin",
        ["Str_Footer_Folder"] = "Folder",
        ["Str_Footer_Actions"] = "Actions",
        ["Str_Footer_Hide"] = "Hide",
        ["Str_Quick_Hotspot"] = "Hotspot",
        ["Str_Quick_Battery"] = "Battery Saver",
        ["Str_Quick_BatteryOn"] = "Battery Saver: On",
        ["Str_Quick_TaskbarCenter"] = "Taskbar: Center",
        ["Str_Quick_TaskbarLeft"] = "Taskbar: Left",
        ["Str_Quick_Screenshot"] = "Screenshot",
        ["Str_Quick_Processes"] = "Processes",
        ["Str_Quick_Wifi"] = "Wi-Fi",
        ["Str_Quick_WifiOn"] = "Wi-Fi: On",
        ["Str_Quick_WifiOff"] = "Wi-Fi: Off",
        ["Str_Quick_Bluetooth"] = "Bluetooth",
        ["Str_Quick_BluetoothOn"] = "Bluetooth: On",
        ["Str_Quick_BluetoothOff"] = "Bluetooth: Off",
        ["Str_Type_App"] = "Application",
        ["Str_Type_Calc"] = "Calculator",
        ["Str_Type_AI"] = "Local AI",
        ["Str_Type_Command"] = "Shell Command",
        ["Str_Type_Url"] = "Web URL",
        ["Str_Type_Bookmark"] = "Bookmark",
        ["Str_Type_Process"] = "Process",
        ["Str_Type_Setting"] = "Setting",
        ["Str_Type_Folder"] = "Folder",
        ["Str_Type_File"] = "File",
        ["Str_Type_Clipboard"] = "Clipboard",
        ["Str_Type_System"] = "System",
        ["Str_Type_Plugin"] = "Plugin",
        ["Str_Nav_General"] = "General",
        ["Str_Nav_Appearance"] = "Appearance",
        ["Str_Nav_Window"] = "Window & Display",
        ["Str_Nav_Apps"] = "Apps & Processes",
        ["Str_Nav_System"] = "Windows Controls",
        ["Str_Nav_AI"] = "Local AI",
        ["Str_Nav_About"] = "About",
        ["Str_Tray_Tooltip"] = "LAILauncher — Fast Windows Search & Control",
        ["Str_Tray_Open"] = "Open LAILauncher (Alt+Space)",
        ["Str_Tray_Settings"] = "Settings",
        ["Str_Tray_Processes"] = "Background Processes",
        ["Str_Tray_SwitchLang"] = "Chuyển sang Tiếng Việt (VI)",
        ["Str_Tray_Exit"] = "Exit"
    };

    public static string NormalizeLanguage(string? langCode)
    {
        if (string.IsNullOrWhiteSpace(langCode)) return Vietnamese;
        var clean = langCode.Trim().ToLowerInvariant();
        return clean.StartsWith("en") ? English : Vietnamese;
    }

    public static void SetLanguage(string? langCode)
    {
        var normalized = NormalizeLanguage(langCode);
        CurrentLanguage = normalized;

        var app = Application.Current;
        if (app != null)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Launcher.App;component/Resources/Strings.{normalized}.xaml", UriKind.Absolute);
                var loaded = new ResourceDictionary { Source = uri };

                var merged = app.Resources.MergedDictionaries;
                if (_activeLanguageDictionary != null)
                {
                    merged.Remove(_activeLanguageDictionary);
                }

                // Also remove any initial static Strings.*.xaml loaded in App.xaml on first switch
                for (int i = merged.Count - 1; i >= 0; i--)
                {
                    var src = merged[i].Source?.OriginalString;
                    if (!string.IsNullOrEmpty(src) && src.Contains("Resources/Strings.", StringComparison.OrdinalIgnoreCase))
                    {
                        merged.RemoveAt(i);
                    }
                }

                merged.Add(loaded);
                _activeLanguageDictionary = loaded;
            }
            catch
            {
                // Safe fallback in unit tests or headless Application without pack URI registration
                var fallbackMap = normalized == English ? FallbackEn : FallbackVi;
                foreach (var kv in fallbackMap)
                {
                    app.Resources[kv.Key] = kv.Value;
                }
            }
        }

        LanguageChanged?.Invoke(normalized);
    }

    public static string Get(string key, string fallback = "")
    {
        if (Application.Current?.TryFindResource(key) is string resourceValue && !string.IsNullOrEmpty(resourceValue))
        {
            return resourceValue;
        }

        var map = CurrentLanguage == English ? FallbackEn : FallbackVi;
        if (map.TryGetValue(key, out var val))
        {
            return val;
        }

        return !string.IsNullOrEmpty(fallback) ? fallback : key;
    }
}
