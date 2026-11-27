using Launcher.SDK;
using Launcher.Core.Utilities;
using Launcher.Core.Actions;

namespace Launcher.Core.Search;

public class QueryPipeline
{
    private readonly List<ISearchProvider> _providers = [];
    private volatile ISearchProvider[] _activeProviders = [];
    private readonly RankingEngine _rankingEngine;
    private readonly object _providerLock = new();

    public QueryPipeline(RankingEngine rankingEngine)
    {
        _rankingEngine = rankingEngine;
    }

    public void RegisterProvider(ISearchProvider provider)
    {
        lock (_providerLock)
        {
            if (!_providers.Contains(provider))
            {
                _providers.Add(provider);
                _activeProviders = _providers.OrderBy(p => p.Priority).ToArray();
            }
        }
    }

    public void UnregisterProvider(ISearchProvider provider)
    {
        lock (_providerLock)
        {
            if (_providers.Remove(provider))
            {
                _activeProviders = _providers.OrderBy(p => p.Priority).ToArray();
            }
        }
    }

    public async Task<List<QueryResult>> ExecuteAsync(string rawQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return [];
        }

        var trimmed = rawQuery.Trim();
        var context = new QueryContext(rawQuery, cancellationToken);
        var results = new List<QueryResult>();

        // 1. Command Mode: Query starts with '>' (CMD) or '>>' (PowerShell)
        if (trimmed.StartsWith('>'))
        {
            bool usePowerShell = trimmed.StartsWith(">>");
            var commandText = usePowerShell
                ? trimmed[2..].Trim()
                : (trimmed.Length > 1 ? trimmed[1..].Trim() : string.Empty);

            if (string.IsNullOrEmpty(commandText))
            {
                results.Add(new QueryResult
                {
                    Id = "cmd_prompt",
                    Title = usePowerShell ? ">> Nhập lệnh PowerShell..." : "> Nhập lệnh CMD / PowerShell...",
                    Subtitle = "Ví dụ: > ipconfig, > ping -n 4 8.8.8.8, >> Get-Date (Enter để chạy ngay)",
                    Score = 1000.0,
                    Type = ResultType.Command,
                    IconGlyph = "💻",
                    Actions = []
                });
            }
            else
            {
                var actions = usePowerShell
                    ? new List<ResultAction>
                    {
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: false, usePowerShell: true),
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: true, usePowerShell: true),
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: false, usePowerShell: false),
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: true, usePowerShell: false)
                    }
                    : new List<ResultAction>
                    {
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: false, usePowerShell: false),
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: true, usePowerShell: false),
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: false, usePowerShell: true),
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: true, usePowerShell: true)
                    };

                results.Add(new QueryResult
                {
                    Id = $"cmd_{commandText.GetHashCode()}",
                    Title = usePowerShell ? $">> {commandText}" : $"> {commandText}",
                    Subtitle = usePowerShell
                        ? "Chạy nhanh PowerShell ngay trên LAILauncher (Enter · Ctrl+Shift+Enter: Admin)"
                        : "Chạy nhanh ngay trên LAILauncher (Enter · Ctrl+Shift+Enter: Admin)",
                    Score = 1000.0,
                    Type = ResultType.Command,
                    IconGlyph = "💻",
                    ShortcutHint = "Enter",
                    Metadata = new Dictionary<string, string>
                    {
                        ["Command"] = commandText,
                        ["Shell"] = usePowerShell ? "powershell" : "cmd"
                    },
                    Actions = actions
                });

                if (!usePowerShell)
                {
                    results.Add(new QueryResult
                    {
                        Id = $"cmd_ps_{commandText.GetHashCode()}",
                        Title = $">> {commandText}",
                        Subtitle = "Chạy nhanh bằng PowerShell ngay trên LAILauncher",
                        Score = 900.0,
                        Type = ResultType.Command,
                        IconGlyph = "💻",
                        ShortcutHint = "Alt+2",
                        Metadata = new Dictionary<string, string>
                        {
                            ["Command"] = commandText,
                            ["Shell"] = "powershell"
                        },
                        Actions =
                        [
                            ActionRunner.CreateCommandAction(commandText, runAsAdmin: false, usePowerShell: true),
                            ActionRunner.CreateCommandAction(commandText, runAsAdmin: true, usePowerShell: true)
                        ]
                    });
                }

                // In command mode, also offer Google Search as fallback
                results.Add(CreateGoogleSearchResult(commandText, isDirectSearch: false));
            }

            return await _rankingEngine.RankAsync(context.RawQuery, results);
        }

        // 2. Calculator & Unit Converter evaluation
        var isCalcKeyword = trimmed.Equals("calc", StringComparison.OrdinalIgnoreCase) 
            || trimmed.Equals("calc:", StringComparison.OrdinalIgnoreCase);

        if (isCalcKeyword)
        {
            results.Add(new QueryResult
            {
                Id = "calc_prompt",
                Title = "🧮 Máy tính & Đổi đơn vị (Calculator)",
                Subtitle = "Nhập biểu thức (Ví dụ: 50 * 20%, (100+50)*10%, sqrt(144), 100 km to mi, 0xFF to dec...)",
                Score = 1000.0,
                Type = ResultType.Calculator,
                IconGlyph = "🧮",
                Actions = []
            });
        }
        else if (CalculatorEvaluator.TryEvaluate(context.RawQuery, out var calcResult, out var formattedResult))
        {
            var cleanExpr = context.RawQuery.Trim();
            if (cleanExpr.StartsWith("calc ", StringComparison.OrdinalIgnoreCase))
                cleanExpr = cleanExpr[5..].Trim();
            else if (cleanExpr.StartsWith("calc:", StringComparison.OrdinalIgnoreCase))
                cleanExpr = cleanExpr[5..].Trim();
            else if (cleanExpr.StartsWith("="))
                cleanExpr = cleanExpr[1..].Trim();

            results.Add(new QueryResult
            {
                Id = "calc_result",
                Title = formattedResult,
                Subtitle = $"= {cleanExpr}",
                Score = 1000.0,
                Type = ResultType.Calculator,
                IconGlyph = "🧮",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Copy to Clipboard",
                        Description = "Copy calculated result to clipboard",
                        IconGlyph = "📋",
                        ShortcutHint = "Enter",
                        SyncAction = () => { ClipboardHelper.SetText(formattedResult); }
                    }
                ]
            });
        }

        // 3. Hardware List Fast-Path (sys wifi list / sys bluetooth list -> show directly in-app without noise)
        var active = _activeProviders;
        if (OperatingSystem.IsWindows() && Launcher.Core.Plugins.SystemCommandsPlugin.IsHardwareListQuery(trimmed))
        {
            var sysProvider = active.FirstOrDefault(p => p.ProviderId == "system_commands");
            if (sysProvider != null)
            {
                var sysResults = await QueryProviderSafeAsync(sysProvider, rawQuery, cancellationToken, timeoutMs: 1500);
                results.AddRange(sysResults);
                return await _rankingEngine.RankAsync(context.RawQuery, results);
            }
        }

        // 4. Query all registered providers concurrently using pre-sorted array
        if (active.Length > 0)
        {
            bool isSysQuery = trimmed.StartsWith("sys ", StringComparison.OrdinalIgnoreCase) ||
                              trimmed.StartsWith("win ", StringComparison.OrdinalIgnoreCase);
            var tasks = new Task<IEnumerable<QueryResult>>[active.Length];
            for (int i = 0; i < active.Length; i++)
            {
                int timeout = (isSysQuery && active[i].ProviderId == "system_commands") ? 1200 : 300;
                tasks[i] = QueryProviderSafeAsync(active[i], rawQuery, cancellationToken, timeout);
            }

            var providerResults = await Task.WhenAll(tasks);
            for (int i = 0; i < providerResults.Length; i++)
            {
                var list = providerResults[i];
                if (list != null)
                {
                    results.AddRange(list);
                }
            }
        }

        // 4. If no explicit web search keyword was matched, add Google Search row as fallback
        var isExplicitWebSearch = trimmed.StartsWith('?') 
            || trimmed.StartsWith("g ", StringComparison.OrdinalIgnoreCase) 
            || trimmed.StartsWith("google ", StringComparison.OrdinalIgnoreCase);

        var webSearchTerm = isExplicitWebSearch
            ? (trimmed.StartsWith('?') ? trimmed[1..].Trim() : (trimmed.StartsWith("g ", StringComparison.OrdinalIgnoreCase) ? trimmed[2..].Trim() : trimmed[7..].Trim()))
            : trimmed;

        bool hasWebSearch = false;
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].SourcePluginId == "web_search")
            {
                hasWebSearch = true;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(webSearchTerm) && !hasWebSearch)
        {
            results.Add(CreateGoogleSearchResult(webSearchTerm, isDirectSearch: isExplicitWebSearch));
        }

        // 5. Rank results
        return await _rankingEngine.RankAsync(context.RawQuery, results);
    }

    private static async Task<IEnumerable<QueryResult>> QueryProviderSafeAsync(ISearchProvider provider, string rawQuery, CancellationToken cancellationToken, int timeoutMs = 300)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var providerContext = new QueryContext(rawQuery, linkedCts.Token);

            var res = await provider.QueryAsync(providerContext);
            return res ?? [];
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception ex)
        {
            // Isolate provider failure
            System.Diagnostics.Debug.WriteLine($"Provider {provider.DisplayName} failed: {ex.Message}");
            return [];
        }
    }

    private static QueryResult CreateGoogleSearchResult(string query, bool isDirectSearch)
    {
        var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
        return new QueryResult
        {
            Id = $"web_google_{query.GetHashCode()}",
            Title = $"Tìm trên Google: \"{query}\"",
            Subtitle = searchUrl,
            Score = isDirectSearch ? 500.0 : 5.0,
            Type = ResultType.Url,
            IconGlyph = "🌐",
            Metadata = new Dictionary<string, string>
            {
                ["Url"] = searchUrl
            },
            Actions =
            [
                ActionRunner.CreateWebSearchAction(query),
                new ResultAction
                {
                    Name = "Copy Search URL",
                    Description = "Copy Google search URL to clipboard",
                    IconGlyph = "📋",
                    SyncAction = () => { ClipboardHelper.SetText(searchUrl); }
                }
            ]
        };
    }
}
