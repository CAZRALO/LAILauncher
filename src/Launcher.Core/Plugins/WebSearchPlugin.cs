using System.Diagnostics;
using Launcher.Core.Utilities;
using Launcher.SDK;

namespace Launcher.Core.Plugins;

public class WebSearchPlugin : ISearchProvider
{
    public string ProviderId => "web_search";
    public string DisplayName => "Web Search Engines";
    public int Priority => 50;
    public string? ActionKeyword => null; // Dynamic multi-keyword
    public bool SupportsGlobalSearch => true;

    public record WebEngine(
        string Id,
        string Name,
        string[] Keywords,
        string UrlTemplate,
        string IconGlyph,
        string Description
    );

    private readonly List<WebEngine> _engines =
    [
        new("engine_google", "Google", ["g", "google"], "https://www.google.com/search?q={0}", "🌐", "Tìm kiếm Google"),
        new("engine_youtube", "YouTube", ["yt", "youtube"], "https://www.youtube.com/results?search_query={0}", "▶️", "Tìm video trên YouTube"),
        new("engine_github", "GitHub", ["gh", "github"], "https://github.com/search?q={0}", "🐙", "Tìm kiếm mã nguồn & kho lưu trữ GitHub"),
        new("engine_wiki", "Wikipedia", ["wiki", "wikipedia"], "https://en.wikipedia.org/wiki/Special:Search?search={0}", "📚", "Tra cứu bách khoa toàn thư Wikipedia"),
        new("engine_duckduckgo", "DuckDuckGo", ["ddg", "duck"], "https://duckduckgo.com/?q={0}", "🦆", "Tìm kiếm riêng tư DuckDuckGo"),
        new("engine_bing", "Bing", ["bing", "bng"], "https://www.bing.com/search?q={0}", "🔷", "Tìm kiếm Microsoft Bing"),
        new("engine_stackoverflow", "Stack Overflow", ["so", "stack", "stackoverflow"], "https://stackoverflow.com/search?q={0}", "🥞", "Tìm hỏi đáp lập trình trên Stack Overflow"),
        new("engine_translate", "Google Dịch", ["trans", "translate", "dich"], "https://translate.google.com/?sl=auto&tl=vi&text={0}&op=translate", "🔤", "Dịch ngôn ngữ Google Translate"),
        new("engine_maps", "Google Maps", ["maps", "map", "bando"], "https://www.google.com/maps/search/{0}", "🗺️", "Tìm địa điểm trên Google Maps"),
        new("engine_nuget", "NuGet", ["nuget"], "https://www.nuget.org/packages?q={0}", "📦", "Tìm kiếm gói .NET NuGet"),
        new("engine_npm", "npm", ["npm"], "https://www.npmjs.com/search?q={0}", "📦", "Tìm kiếm gói JavaScript / Node npm"),
        new("engine_pip", "PyPI", ["pip", "pypi"], "https://pypi.org/search/?q={0}", "🐍", "Tìm kiếm gói Python PyPI"),
        new("engine_reddit", "Reddit", ["reddit", "rd"], "https://www.reddit.com/search/?q={0}", "🤖", "Tìm bài viết và cộng đồng trên Reddit")
    ];

    public IReadOnlyList<WebEngine> AvailableEngines => _engines;

    public Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context)
    {
        var raw = context.RawQuery.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return Task.FromResult<IEnumerable<QueryResult>>([]);

        var results = new List<QueryResult>();

        // Check if query matches specific prefix: e.g. "yt lofi chill", "gh dotnet", "trans hello world"
        var firstSpace = raw.IndexOf(' ');
        var keyword = firstSpace > 0 ? raw[..firstSpace].ToLowerInvariant() : raw.ToLowerInvariant();
        var searchParam = firstSpace > 0 ? raw[(firstSpace + 1)..].Trim() : string.Empty;

        // 1. Direct Keyword Match
        var matchedEngine = _engines.FirstOrDefault(e => e.Keywords.Contains(keyword));
        if (matchedEngine != null)
        {
            if (string.IsNullOrEmpty(searchParam))
            {
                results.Add(new QueryResult
                {
                    Id = $"{matchedEngine.Id}_prompt",
                    Title = $"Tìm kiếm trên {matchedEngine.Name}...",
                    Subtitle = $"Gõ từ khóa sau '{keyword} ' để tìm kiếm",
                    Score = 900.0,
                    Type = ResultType.Url,
                    IconGlyph = matchedEngine.IconGlyph,
                    SourcePluginId = "web_search",
                    Actions = []
                });
            }
            else
            {
                var targetUrl = string.Format(matchedEngine.UrlTemplate, Uri.EscapeDataString(searchParam));
                results.Add(new QueryResult
                {
                    Id = $"{matchedEngine.Id}_{searchParam.GetHashCode()}",
                    Title = $"Tìm trên {matchedEngine.Name}: \"{searchParam}\"",
                    Subtitle = targetUrl,
                    Score = 950.0,
                    Type = ResultType.Url,
                    IconGlyph = matchedEngine.IconGlyph,
                    SourcePluginId = "web_search",
                    Metadata = new Dictionary<string, string>
                    {
                        ["Url"] = targetUrl,
                        ["Query"] = searchParam
                    },
                    Actions =
                    [
                        CreateOpenUrlAction(targetUrl, $"Mở tìm kiếm {matchedEngine.Name}"),
                        CreateCopyUrlAction(targetUrl)
                    ]
                });
            }
        }

        return Task.FromResult<IEnumerable<QueryResult>>(results);
    }

    private static ResultAction CreateOpenUrlAction(string url, string title)
    {
        return new ResultAction
        {
            Name = title,
            Description = url,
            IconGlyph = "🌐",
            ShortcutHint = "Enter",
            SyncAction = () =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebSearchPlugin] Failed to open URL {url}: {ex.Message}");
                }
            }
        };
    }

    private static ResultAction CreateCopyUrlAction(string url)
    {
        return new ResultAction
        {
            Name = "Sao chép liên kết URL",
            Description = "Copy URL vào Clipboard",
            IconGlyph = "📋",
            ShortcutHint = "Ctrl+C",
            SyncAction = () =>
            {
                ClipboardHelper.SetText(url);
            }
        };
    }
}
