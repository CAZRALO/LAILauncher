using Launcher.SDK;
using Launcher.Core.Search;
using Launcher.Core.Actions;
using Launcher.Database.Models;
using Launcher.Database.Repositories;
using System.Diagnostics;
using System.IO;

namespace Launcher.Indexer;

public class AppIndexer : ISearchProvider
{
    private sealed class CachedAppEntry
    {
        public required AppEntity App { get; init; }
        public required string[] Keywords { get; init; }
        public required string Subtitle { get; init; }
        public required string Glyph { get; init; }
        public required double LaunchBonus { get; init; }
        public required List<ResultAction> Actions { get; init; }
        public required string? UnaccentName { get; init; }
    }

    private readonly IAppRepository _appRepository;
    private readonly List<AppEntity> _cachedApps = [];
    private volatile CachedAppEntry[] _cachedEntries = [];
    private readonly object _lock = new();

    public string ProviderId => "core.app_indexer";
    public string DisplayName => "Applications";
    public int Priority => 1;

    public AppIndexer(IAppRepository appRepository)
    {
        _appRepository = appRepository;
    }

    private void UpdateCache(IEnumerable<AppEntity> apps)
    {
        lock (_lock)
        {
            _cachedApps.Clear();
            _cachedApps.AddRange(apps);
            _cachedEntries = _cachedApps.Select(app =>
            {
                var hasNonAscii = Launcher.Core.Utilities.VietnameseTextHelper.HasNonAscii(app.Name.AsSpan());
                return new CachedAppEntry
                {
                    App = app,
                    Keywords = AppKeywordHelper.GetKeywords(app.Name, app.Path).ToArray(),
                    Subtitle = FormatSubtitle(app),
                    Glyph = GetGlyphForApp(app),
                    LaunchBonus = Math.Min(25.0, app.LaunchCount * 3.0),
                    Actions = BuildActions(app),
                    UnaccentName = hasNonAscii ? Launcher.Core.Utilities.VietnameseTextHelper.RemoveDiacritics(app.Name) : app.Name
                };
            }).ToArray();
        }
    }

    public async Task InitializeAsync()
    {
        // 1. Load existing apps from DB cache for instant startup
        var existing = await _appRepository.GetAllAsync();
        UpdateCache(existing);

        // 2. Scan all system apps in background without blocking startup
        _ = Task.Run(async () =>
        {
            try
            {
                await ScanAllAppsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning apps: {ex.Message}");
            }
        });
    }

    public async Task ScanAllAppsAsync()
    {
        var allApps = new List<AppEntity>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan Windows Shell AppsFolder (Store / UWP apps like Notepad, Arc, Scrble Ink, CPU-Info, Calculator, etc.)
        try
        {
            var appsFolderApps = await AppScannerHelper.ScanAppsFolderAsync();
            foreach (var app in appsFolderApps)
            {
                if (seenPaths.Add(app.Path))
                {
                    seenNames.Add(app.Name);
                    allApps.Add(app);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppsFolder scan error: {ex.Message}");
        }

        // 2. Scan Guaranteed Built-in Windows System Tools
        try
        {
            var builtIns = AppScannerHelper.GetBuiltInSystemApps();
            foreach (var app in builtIns)
            {
                if (seenPaths.Add(app.Path) && seenNames.Add(app.Name))
                {
                    allApps.Add(app);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Built-in tools scan error: {ex.Message}");
        }

        // 3. Scan Start Menu Shortcuts (.lnk and .url)
        try
        {
            var startMenuApps = AppScannerHelper.ScanStartMenuShortcuts();
            foreach (var app in startMenuApps)
            {
                if (seenPaths.Add(app.Path) && seenNames.Add(app.Name))
                {
                    allApps.Add(app);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Start Menu scan error: {ex.Message}");
        }

        // 4. Scan Desktop Shortcuts (.lnk and .url)
        try
        {
            var desktopApps = AppScannerHelper.ScanDesktopShortcuts();
            foreach (var app in desktopApps)
            {
                if (seenPaths.Add(app.Path) && seenNames.Add(app.Name))
                {
                    allApps.Add(app);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Desktop scan error: {ex.Message}");
        }

        // 5. Scan Registry App Paths (Chrome, VS Code, etc.)
        try
        {
            var regApps = AppScannerHelper.ScanRegistryAppPaths();
            foreach (var app in regApps)
            {
                if (seenPaths.Add(app.Path) && seenNames.Add(app.Name))
                {
                    allApps.Add(app);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Registry App Paths scan error: {ex.Message}");
        }

        if (allApps.Count > 0)
        {
            await _appRepository.UpsertBatchAsync(allApps);
            var updated = await _appRepository.GetAllAsync();
            UpdateCache(updated);
        }
    }

    public Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context)
    {
        var query = !string.IsNullOrWhiteSpace(context.SearchTerm) ? context.SearchTerm : context.RawQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IEnumerable<QueryResult>>([]);
        }

        var entries = _cachedEntries;
        if (entries.Length == 0)
        {
            return Task.FromResult<IEnumerable<QueryResult>>([]);
        }

        var results = new List<QueryResult>();
        var unaccentQuery = Launcher.Core.Utilities.VietnameseTextHelper.HasNonAscii(query.AsSpan())
            ? Launcher.Core.Utilities.VietnameseTextHelper.RemoveDiacritics(query)
            : query;

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var nameScore = FuzzyMatcher.Score(entry.App.Name, query, unaccentQuery, entry.UnaccentName);

            // Match against keywords, aliases, executable tokens from pre-indexed cache
            var bestKeywordScore = 0.0;
            var keywords = entry.Keywords;
            for (int k = 0; k < keywords.Length; k++)
            {
                var kwScore = FuzzyMatcher.Score(keywords[k], query, unaccentQuery);
                if (kwScore > bestKeywordScore)
                {
                    bestKeywordScore = kwScore;
                }
            }

            var finalScore = Math.Max(nameScore, bestKeywordScore * 0.92);

            if (finalScore >= 30.0)
            {
                results.Add(new QueryResult
                {
                    Id = entry.App.Id,
                    Title = entry.App.Name,
                    Subtitle = entry.Subtitle,
                    Score = finalScore + 20.0 + entry.LaunchBonus,
                    Type = ResultType.App,
                    IconPath = entry.App.IconPath ?? entry.App.Path,
                    IconGlyph = entry.Glyph,
                    Metadata = new Dictionary<string, string>
                    {
                        ["Path"] = entry.App.Path,
                        ["Arguments"] = entry.App.Arguments ?? string.Empty
                    },
                    Actions = entry.Actions
                });
            }
        }

        return Task.FromResult<IEnumerable<QueryResult>>(results);
    }

    private static string FormatSubtitle(AppEntity app)
    {
        if (app.Path.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
        {
            return $"Ứng dụng Windows • {app.Name}";
        }

        if (app.Source == "System")
        {
            return $"Công cụ hệ thống Windows";
        }

        return app.Path;
    }

    private static string GetGlyphForApp(AppEntity app)
    {
        var n = app.Name;
        if (n.Contains("CPU", StringComparison.OrdinalIgnoreCase)) return "⚡";
        if (n.Contains("Note", StringComparison.OrdinalIgnoreCase) || n.Contains("Ink", StringComparison.OrdinalIgnoreCase)) return "📝";
        if (n.Contains("Arc", StringComparison.OrdinalIgnoreCase) || n.Contains("Browser", StringComparison.OrdinalIgnoreCase) || n.Contains("Chrome", StringComparison.OrdinalIgnoreCase) || n.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return "🌐";
        if (n.Contains("Calc", StringComparison.OrdinalIgnoreCase)) return "🧮";
        if (n.Contains("Paint", StringComparison.OrdinalIgnoreCase)) return "🎨";
        if (n.Contains("Terminal", StringComparison.OrdinalIgnoreCase) || n.Contains("Command", StringComparison.OrdinalIgnoreCase) || n.Contains("PowerShell", StringComparison.OrdinalIgnoreCase)) return "💻";
        if (n.Contains("Task Manager", StringComparison.OrdinalIgnoreCase)) return "📊";
        if (n.Contains("Setting", StringComparison.OrdinalIgnoreCase) || n.Contains("Control", StringComparison.OrdinalIgnoreCase)) return "⚙️";
        return "💻";
    }

    private static List<ResultAction> BuildActions(AppEntity app)
    {
        var actions = new List<ResultAction>
        {
            ActionRunner.CreateProcessAction(app.Path, app.Arguments, runAsAdmin: false, customName: "Mở " + app.Name),
            ActionRunner.CreateProcessAction(app.Path, app.Arguments, runAsAdmin: true, customName: "Chạy dưới quyền Administrator")
        };

        if (File.Exists(app.Path))
        {
            actions.Add(ActionRunner.CreateOpenFolderAction(app.Path));
        }

        actions.Add(ActionRunner.CreateCopyPathAction(app.Path));
        return actions;
    }
}
