namespace Launcher.SDK;

public class QueryContext
{
    public string RawQuery { get; }
    public string SearchTerm { get; }
    public string? ActionKeyword { get; }
    public CancellationToken CancellationToken { get; }
    public Dictionary<string, object> Parameters { get; }

    private static readonly HashSet<string> KnownKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "sys", "set", "settings", "b", "bm", "kill", "calc", "g", "yt", "gh", "wiki", "ddg", "bing", "so", "trans", "maps", "nuget", "npm", "pip", "rd", "reddit"
    };

    public QueryContext(string rawQuery, CancellationToken cancellationToken = default)
    {
        RawQuery = rawQuery;
        CancellationToken = cancellationToken;
        Parameters = [];

        var trimmed = rawQuery.Trim();
        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace > 0)
        {
            var potentialKeyword = trimmed[..firstSpace];
            if (KnownKeywords.Contains(potentialKeyword))
            {
                ActionKeyword = potentialKeyword;
                SearchTerm = trimmed[(firstSpace + 1)..].TrimStart();
                return;
            }
        }

        ActionKeyword = null;
        SearchTerm = trimmed;
    }
}
