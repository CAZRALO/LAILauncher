using Launcher.SDK;
using Launcher.Core.Utilities;
using Launcher.Core.Actions;

namespace Launcher.Core.Search;

public class QueryPipeline
{
    private readonly List<ISearchProvider> _providers = [];
    private readonly RankingEngine _rankingEngine;

    public QueryPipeline(RankingEngine rankingEngine)
    {
        _rankingEngine = rankingEngine;
    }

    public void RegisterProvider(ISearchProvider provider)
    {
        if (!_providers.Contains(provider))
        {
            _providers.Add(provider);
        }
    }

    public void UnregisterProvider(ISearchProvider provider)
    {
        _providers.Remove(provider);
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

        // 1. Command Mode: Query starts with '>'
        if (trimmed.StartsWith('>'))
        {
            var commandText = trimmed.Length > 1 ? trimmed[1..].Trim() : string.Empty;
            if (string.IsNullOrEmpty(commandText))
            {
                results.Add(new QueryResult
                {
                    Id = "cmd_prompt",
                    Title = "> Nhập lệnh CMD / PowerShell...",
                    Subtitle = "Ví dụ: > ping google.com, > ipconfig /all, > code .",
                    Score = 1000.0,
                    Type = ResultType.Command,
                    IconGlyph = "💻",
                    Actions = []
                });
            }
            else
            {
                results.Add(new QueryResult
                {
                    Id = $"cmd_{commandText.GetHashCode()}",
                    Title = $"> {commandText}",
                    Subtitle = $"Thực thi '{commandText}' trong Terminal (cmd.exe /k)",
                    Score = 1000.0,
                    Type = ResultType.Command,
                    IconGlyph = "💻",
                    Metadata = new Dictionary<string, string>
                    {
                        ["Command"] = commandText
                    },
                    Actions =
                    [
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: false, usePowerShell: false),
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: true, usePowerShell: false),
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: false, usePowerShell: true),
                        ActionRunner.CreateCommandAction(commandText, runAsAdmin: true, usePowerShell: true)
                    ]
                });

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
                        SyncAction = () => { /* Handled in clipboard */ }
                    }
                ]
            });
        }

        // 3. Query all registered providers concurrently
        var activeProviders = _providers.OrderBy(p => p.Priority).ToList();
        var tasks = activeProviders.Select(async provider =>
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
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
        });

        var providerResults = await Task.WhenAll(tasks);
        foreach (var list in providerResults)
        {
            results.AddRange(list);
        }

        // 4. If no explicit web search keyword was matched, add Google Search row as fallback
        var isExplicitWebSearch = trimmed.StartsWith('?') 
            || trimmed.StartsWith("g ", StringComparison.OrdinalIgnoreCase) 
            || trimmed.StartsWith("google ", StringComparison.OrdinalIgnoreCase);

        var webSearchTerm = isExplicitWebSearch
            ? (trimmed.StartsWith('?') ? trimmed[1..].Trim() : (trimmed.StartsWith("g ", StringComparison.OrdinalIgnoreCase) ? trimmed[2..].Trim() : trimmed[7..].Trim()))
            : trimmed;

        if (!string.IsNullOrWhiteSpace(webSearchTerm) && !results.Any(r => r.SourcePluginId == "web_search"))
        {
            results.Add(CreateGoogleSearchResult(webSearchTerm, isDirectSearch: isExplicitWebSearch));
        }

        // 5. Rank results
        return await _rankingEngine.RankAsync(context.RawQuery, results);
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
                    SyncAction = () => { /* Handled in clipboard */ }
                }
            ]
        };
    }
}
