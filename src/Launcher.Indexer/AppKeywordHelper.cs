using System.IO;

namespace Launcher.Indexer;

public static class AppKeywordHelper
{
    private static readonly Dictionary<string, string[]> AppAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Notepad"] = ["ghi chú", "soạn thảo", "text editor", "note", "notepad.exe", "txt", "editor"],
        ["Scrble Ink"] = ["ghi chú", "vẽ", "bút", "handwriting", "drawing", "notes", "scribble", "ink", "scrble"],
        ["CPU-Info"] = ["cpu", "hardware", "thông tin cpu", "phần cứng", "processor", "system info", "cpu info", "cpuinfo"],
        ["CPU-Z"] = ["cpu", "hardware", "thông số máy", "phần cứng", "cpuid", "processor", "cpuz", "cpu-z"],
        ["Arc"] = ["browser", "trình duyệt", "web", "internet", "the browser company", "arc browser"],
        ["Google Chrome"] = ["browser", "trình duyệt", "web", "gg chrome", "chrome", "google"],
        ["Mozilla Firefox"] = ["browser", "trình duyệt", "web", "firefox", "ff"],
        ["Brave"] = ["browser", "trình duyệt", "web", "brave browser"],
        ["Microsoft Edge"] = ["browser", "trình duyệt", "web", "edge"],
        ["Calculator"] = ["máy tính", "calc", "casio", "tính toán", "calculator.exe"],
        ["Paint"] = ["vẽ", "bản vẽ", "mspaint", "draw", "chỉnh sửa ảnh", "image editor"],
        ["Command Prompt"] = ["cmd", "terminal", "console", "dòng lệnh", "cmd.exe"],
        ["Windows Terminal"] = ["terminal", "wt", "powershell", "cmd", "console"],
        ["PowerShell"] = ["powershell", "ps", "terminal", "script", "powershell.exe"],
        ["Task Manager"] = ["taskmgr", "quản lý tác vụ", "tiến trình", "process", "task manager", "taskmgr.exe"],
        ["Settings"] = ["cài đặt", "hệ thống", "ms-settings", "configuration", "tùy chọn", "setting"],
        ["Control Panel"] = ["bảng điều khiển", "cài đặt", "control", "control.exe"],
        ["Device Manager"] = ["quản lý thiết bị", "driver", "devmgmt.msc"],
        ["Disk Management"] = ["quản lý ổ đĩa", "phân vùng", "diskmgmt.msc"],
        ["Registry Editor"] = ["regedit", "chỉnh sửa registry", "regedit.exe"],
        ["Resource Monitor"] = ["theo dõi tài nguyên", "resmon", "ram", "cpu", "disk"],
        ["System Information"] = ["thông tin hệ thống", "msinfo32", "cấu hình máy"],
        ["Snipping Tool"] = ["chụp màn hình", "screenshot", "snip", "cắt ảnh"],
        ["File Explorer"] = ["quản lý tệp", "explorer", "thư mục", "ổ đĩa", "explorer.exe"],
        ["Visual Studio Code"] = ["vscode", "code", "lập trình", "editor"],
        ["Visual Studio"] = ["vs", "ide", "lập trình"]
    };

    public static List<string> GetKeywords(string appName, string? path = null)
    {
        var keywords = new List<string>();

        // 1. Check dictionary aliases
        foreach (var (key, list) in AppAliases)
        {
            if (appName.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                key.Contains(appName, StringComparison.OrdinalIgnoreCase))
            {
                keywords.AddRange(list);
            }
        }

        // 2. Extract tokens from path or AUMID
        if (!string.IsNullOrWhiteSpace(path))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(fileName) && !fileName.Equals(appName, StringComparison.OrdinalIgnoreCase))
            {
                keywords.Add(fileName);
            }

            // Extract AppUserModelId tokens (e.g. TheBrowserCompany.Arc_... -> Arc, TheBrowserCompany)
            if (path.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
            {
                var aumid = path["shell:AppsFolder\\".Length..];
                var exclamationIdx = aumid.IndexOf('!');
                if (exclamationIdx > 0)
                {
                    keywords.Add(aumid[(exclamationIdx + 1)..]);
                }
                var packageFamily = exclamationIdx > 0 ? aumid[..exclamationIdx] : aumid;
                var dotIdx = packageFamily.IndexOf('.');
                if (dotIdx > 0)
                {
                    var vendor = packageFamily[..dotIdx];
                    keywords.Add(vendor);
                }
            }
        }

        return keywords;
    }
}
