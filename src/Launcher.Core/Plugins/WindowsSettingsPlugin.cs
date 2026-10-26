using System.Diagnostics;
using Launcher.Core.Search;
using Launcher.SDK;

namespace Launcher.Core.Plugins;

public class WindowsSettingsPlugin : ISearchProvider
{
    public string ProviderId => "windows_settings";
    public string DisplayName => "Windows Settings";
    public int Priority => 30;
    public string? ActionKeyword => "set";
    public bool SupportsGlobalSearch => true;

    private record SettingItem(
        string Id,
        string Title,
        string Subtitle,
        string UriScheme,
        string IconGlyph,
        string[] Keywords
    );

    private readonly List<SettingItem> _settings;

    public WindowsSettingsPlugin()
    {
        _settings =
        [
            new("ws_display", "Display Settings", "Độ phân giải, tỷ lệ màn hình, nhiều màn hình", "ms-settings:display", "🖥️", ["display", "man hinh", "resolution", "monitor", "scale", "screen"]),
            new("ws_nightlight", "Night Light", "Bộ lọc ánh sáng xanh bảo vệ mắt", "ms-settings:nightlight", "🌙", ["night light", "anh sang xanh", "blue light", "eye protection"]),
            new("ws_sound", "Sound Settings", "Thiết bị âm thanh, đầu ra tai nghe/loa, micro", "ms-settings:sound", "🔊", ["sound", "audio", "am thanh", "speaker", "loa", "microphone", "volume"]),
            new("ws_notifications", "Notifications & Actions", "Cài đặt thông báo ứng dụng và Focus Assist", "ms-settings:notifications", "🔔", ["notifications", "thong bao", "focus assist", "alerts"]),
            new("ws_powersleep", "Power & Sleep", "Thời gian tắt màn hình và chế độ ngủ", "ms-settings:powersleep", "🔋", ["power", "sleep", "ngu", "pin", "energy", "man hinh tat"]),
            new("ws_battery", "Battery Settings", "Tiết kiệm pin và mức sử dụng năng lượng", "ms-settings:batterysaver", "🪫", ["battery", "pin", "power save", "tiet kiem pin"]),
            new("ws_storage", "Storage", "Dung lượng ổ đĩa và dọn dẹp dung lượng tự động", "ms-settings:storagesense", "💾", ["storage", "o dia", "clean disk", "dung luong", "drive space"]),
            new("ws_bluetooth", "Bluetooth & Devices", "Kết nối tai nghe, chuột, bàn phím Bluetooth", "ms-settings:bluetooth", "📶", ["bluetooth", "thiet bi", "connect", "tai nghe", "chuot"]),
            new("ws_printers", "Printers & Scanners", "Quản lý máy in và máy quét", "ms-settings:printers", "🖨️", ["printer", "may in", "scanner", "scan"]),
            new("ws_mouse", "Mouse Settings", "Tốc độ con trỏ chuột, cuộn chuột", "ms-settings:mousetouchpad", "🖱️", ["mouse", "chuot", "cursor", "pointer", "touchpad"]),
            new("ws_keyboard", "Typing & Keyboard", "Bàn phím, gợi ý từ, kiểm tra chính tả", "ms-settings:typing", "⌨️", ["keyboard", "ban phim", "typing", "phim"]),
            new("ws_wifi", "Wi-Fi Settings", "Mạng không dây và danh sách Wi-Fi khả dụng", "ms-settings:network-wifi", "📡", ["wifi", "wi-fi", "mang khong day", "wireless", "internet"]),
            new("ws_ethernet", "Ethernet Settings", "Cấu hình mạng có dây", "ms-settings:network-ethernet", "🌐", ["ethernet", "lan", "mang day", "network"]),
            new("ws_vpn", "VPN Settings", "Thêm và quản lý kết nối mạng riêng ảo VPN", "ms-settings:network-vpn", "🛡️", ["vpn", "virtual private network", "proxy"]),
            new("ws_background", "Desktop Background", "Hình nền máy tính và ảnh nền màn hình khóa", "ms-settings:personalization-background", "🖼️", ["background", "hinh nen", "wallpaper", "desktop"]),
            new("ws_colors", "Colors & Dark Mode", "Chế độ giao diện Sáng / Tối (Light / Dark mode), màu nhấn", "ms-settings:personalization-colors", "🎨", ["colors", "dark mode", "mau sac", "light mode", "theme"]),
            new("ws_taskbar", "Taskbar Settings", "Ẩn/hiện taskbar, căn chỉnh icon taskbar", "ms-settings:taskbar", "📌", ["taskbar", "thanh tac vu", "dock"]),
            new("ws_apps", "Installed Apps", "Quản lý và gỡ cài đặt ứng dụng (Apps & Features)", "ms-settings:appsfeatures", "📦", ["apps", "applications", "ung dung", "uninstall", "go cai dat", "programs"]),
            new("ws_defaultapps", "Default Apps", "Thiết lập ứng dụng mặc định cho trình duyệt, video, nhạc", "ms-settings:defaultapps", "⭐", ["default apps", "ung dung mac dinh", "browser default"]),
            new("ws_startup", "Startup Apps", "Quản lý các ứng dụng tự khởi động cùng Windows", "ms-settings:startupapps", "🚀", ["startup", "khoi dong cung windows", "autostart", "boot apps"]),
            new("ws_datetime", "Date & Time", "Cài đặt ngày giờ, múi giờ tự động", "ms-settings:dateandtime", "🕒", ["date", "time", "gio", "ngay", "timezone", "dong ho"]),
            new("ws_language", "Language & Region", "Ngôn ngữ hiển thị Windows và bố cục bàn phím", "ms-settings:regionlanguage", "🌐", ["language", "ngon ngu", "region", "tieng viet", "ban phim"]),
            new("ws_update", "Windows Update", "Kiểm tra và cài đặt các bản cập nhật Windows", "ms-settings:windowsupdate", "🔄", ["update", "windows update", "cap nhat", "patch"]),
            new("ws_defender", "Windows Security", "Bảo mật chống virus & đe dọa (Windows Defender)", "ms-settings:windowsdefender", "🛡️", ["security", "defender", "antivirus", "bao mat", "virus"]),
            new("ws_privacy", "Privacy & Security", "Quyền riêng tư vị trí, camera, microphone", "ms-settings:privacy", "👁️", ["privacy", "quyen rieng tu", "camera", "mic", "location"])
        ];
    }

    public Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context)
    {
        var raw = context.RawQuery.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return Task.FromResult<IEnumerable<QueryResult>>([]);

        var query = raw;
        var isExplicitKeyword = false;

        if (query.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            isExplicitKeyword = true;
            query = query[4..].Trim();
        }
        else if (query.Equals("set", StringComparison.OrdinalIgnoreCase) || query.Equals("settings", StringComparison.OrdinalIgnoreCase))
        {
            isExplicitKeyword = true;
            query = string.Empty;
        }

        var results = new List<QueryResult>();

        foreach (var setting in _settings)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            if (isExplicitKeyword && string.IsNullOrEmpty(query))
            {
                results.Add(CreateResult(setting, 100.0));
                continue;
            }

            var titleScore = FuzzyMatcher.Score(setting.Title, query);
            var bestKwScore = 0.0;
            foreach (var kw in setting.Keywords)
            {
                var s = FuzzyMatcher.Score(kw, query);
                if (s > bestKwScore) bestKwScore = s;
            }

            var matchScore = Math.Max(titleScore, bestKwScore);
            if (isExplicitKeyword)
            {
                matchScore = Math.Max(matchScore, 45.0);
            }

            if (matchScore >= 35.0)
            {
                var finalScore = isExplicitKeyword ? matchScore + 130.0 : matchScore + 20.0;
                results.Add(CreateResult(setting, finalScore));
            }
        }

        return Task.FromResult<IEnumerable<QueryResult>>(results);
    }

    private static QueryResult CreateResult(SettingItem setting, double score)
    {
        return new QueryResult
        {
            Id = setting.Id,
            Title = setting.Title,
            Subtitle = setting.Subtitle,
            Score = score,
            Type = ResultType.Setting,
            IconGlyph = setting.IconGlyph,
            SourcePluginId = "windows_settings",
            Metadata = new Dictionary<string, string>
            {
                ["Uri"] = setting.UriScheme
            },
            Actions =
            [
                new ResultAction
                {
                    Name = $"Open {setting.Title}",
                    Description = $"Open {setting.UriScheme}",
                    IconGlyph = setting.IconGlyph,
                    ShortcutHint = "Enter",
                    SyncAction = () =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = setting.UriScheme,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[WindowsSettingsPlugin] Failed to launch {setting.UriScheme}: {ex.Message}");
                        }
                    }
                }
            ]
        };
    }
}
