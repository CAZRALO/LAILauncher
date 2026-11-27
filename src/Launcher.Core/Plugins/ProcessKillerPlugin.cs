using System.Diagnostics;
using Launcher.Core.Search;
using Launcher.SDK;

namespace Launcher.Core.Plugins;

public class ProcessKillerPlugin : ISearchProvider
{
    public string ProviderId => "process_killer";
    public string DisplayName => "Process Killer";
    public int Priority => 40;
    public string? ActionKeyword => "kill";
    public bool SupportsGlobalSearch => true;

    private record ProcessInfoItem(
        int Id,
        string ProcessName,
        long WorkingSet64,
        string? MainModulePath
    );

    private static readonly string[] Prefixes = [
        "trinh chay ngam ", "chay ngam ", "tien trinh ", "tat ung dung ", "dong ung dung ",
        "tat trinh chay ngam ", "task manager ", "taskmgr ", "process ", "proc ", "kill "
    ];

    private static readonly string[] ExactKeywords = [
        "trinh chay ngam", "chay ngam", "ngam", "tien trinh", "task manager",
        "taskmgr", "process", "proc", "kill", "dong ung dung", "tat ung dung",
        "tat trinh chay ngam", "quan ly tac vu"
    ];

    public Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context)
    {
        var raw = context.RawQuery.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return Task.FromResult<IEnumerable<QueryResult>>([]);

        var query = raw;
        var isExplicitKeyword = false;

        foreach (var prefix in Prefixes)
        {
            if (query.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                isExplicitKeyword = true;
                query = query[prefix.Length..].Trim();
                break;
            }
        }

        if (!isExplicitKeyword)
        {
            foreach (var kw in ExactKeywords)
            {
                if (query.Equals(kw, StringComparison.OrdinalIgnoreCase))
                {
                    isExplicitKeyword = true;
                    query = string.Empty;
                    break;
                }
            }
        }

        // Only search processes if explicit keyword OR query starts with process keywords
        if (!isExplicitKeyword)
        {
            return Task.FromResult<IEnumerable<QueryResult>>([]);
        }

        var processItems = GetRunningProcesses();
        var results = new List<QueryResult>();

        foreach (var p in processItems)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            var memMb = p.WorkingSet64 / (1024.0 * 1024.0);
            var memText = memMb >= 1024.0 ? $"{memMb / 1024.0:F2} GB" : $"{memMb:F1} MB";

            if (string.IsNullOrEmpty(query))
            {
                results.Add(CreateResult(p, memText, 100.0));
                continue;
            }

            var nameScore = FuzzyMatcher.Score(p.ProcessName, query);
            var pidScore = p.Id.ToString().Contains(query) ? 80.0 : 0.0;
            var matchScore = Math.Max(nameScore, pidScore);

            if (matchScore >= 35.0)
            {
                results.Add(CreateResult(p, memText, matchScore + 100.0));
            }
        }

        // Order by Score then by RAM usage descending
        var sorted = results
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Metadata.TryGetValue("RAM_Bytes", out var b) && long.TryParse(b, out var bytes) ? bytes : 0)
            .Take(30);

        return Task.FromResult<IEnumerable<QueryResult>>(sorted);
    }

    private static List<ProcessInfoItem> GetRunningProcesses()
    {
        var list = new List<ProcessInfoItem>();
        try
        {
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    string? modulePath = null;
                    try
                    {
                        modulePath = p.MainModule?.FileName;
                    }
                    catch
                    {
                        // Access denied on system processes is expected
                    }

                    list.Add(new ProcessInfoItem(p.Id, p.ProcessName, p.WorkingSet64, modulePath));
                }
                catch
                {
                    // Process might have exited
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessKillerPlugin] Failed to enumerate processes: {ex.Message}");
        }

        return list;
    }

    private static readonly HashSet<string> CriticalSystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "dwm", "explorer"
    };

    private static bool IsCriticalProcess(int pid, string processName)
    {
        if (pid <= 4 || pid == Environment.ProcessId)
            return true;

        return CriticalSystemProcesses.Contains(processName);
    }

    private static QueryResult CreateResult(ProcessInfoItem p, string memText, double score)
    {
        var isCritical = IsCriticalProcess(p.Id, p.ProcessName);
        var subtitle = $"PID: {p.Id} • RAM: {memText}" + (!string.IsNullOrEmpty(p.MainModulePath) ? $" • {p.MainModulePath}" : "") + (isCritical ? " • [Hệ thống]" : "");
        return new QueryResult
        {
            Id = $"proc_{p.Id}_{p.ProcessName}",
            Title = $"Kill {p.ProcessName} (PID: {p.Id})",
            Subtitle = subtitle,
            Score = score,
            Type = ResultType.Process,
            IconGlyph = isCritical ? "⚠️" : "🛑",
            SourcePluginId = "process_killer",
            Metadata = new Dictionary<string, string>
            {
                ["PID"] = p.Id.ToString(),
                ["ProcessName"] = p.ProcessName,
                ["RAM_Bytes"] = p.WorkingSet64.ToString(),
                ["IsCritical"] = isCritical.ToString()
            },
            Actions =
            [
                new ResultAction
                {
                    Name = isCritical ? $"Bảo vệ: {p.ProcessName}" : $"Kill {p.ProcessName}",
                    Description = isCritical ? $"Tiến trình hệ thống quan trọng (Không thể đóng)" : $"Đóng tiến trình PID {p.Id}",
                    IconGlyph = isCritical ? "⚠️" : "🛑",
                    ShortcutHint = "Enter",
                    IsDangerous = !isCritical,
                    SyncAction = () =>
                    {
                        if (isCritical)
                        {
                            Debug.WriteLine($"[ProcessKillerPlugin] Blocked attempt to kill critical process: {p.ProcessName}");
                            return;
                        }

                        try
                        {
                            using var target = Process.GetProcessById(p.Id);
                            target.Kill(entireProcessTree: true);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ProcessKillerPlugin] Kill failed: {ex.Message}");
                        }
                    }
                },
                new ResultAction
                {
                    Name = isCritical ? "Tiến trình được bảo vệ" : "Force Kill as Admin (taskkill /F /PID)",
                    Description = isCritical ? "Không thể dùng Force Kill đối với tiến trình hệ thống quan trọng" : $"Buộc đóng tiến trình PID {p.Id} với quyền quản trị",
                    IconGlyph = isCritical ? "⚠️" : "🛡️",
                    ShortcutHint = "Ctrl+Shift+Enter",
                    IsDangerous = !isCritical,
                    SyncAction = () =>
                    {
                        if (isCritical)
                        {
                            Debug.WriteLine($"[ProcessKillerPlugin] Blocked attempt to force kill critical process: {p.ProcessName}");
                            return;
                        }

                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "taskkill.exe",
                                Arguments = $"/F /PID {p.Id} /T",
                                Verb = "runas",
                                UseShellExecute = true
                            });
                        }
                        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
                        {
                            // UAC canceled
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ProcessKillerPlugin] Admin kill failed: {ex.Message}");
                        }
                    }
                }
            ]
        };
    }
}
