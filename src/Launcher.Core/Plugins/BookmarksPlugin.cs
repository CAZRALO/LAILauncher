using System.Diagnostics;
using System.Text.Json;
using Launcher.Core.Search;
using Launcher.Core.Utilities;
using Launcher.SDK;

namespace Launcher.Core.Plugins;

public class BookmarksPlugin : ISearchProvider
{
    public string ProviderId => "browser_bookmarks";
    public string DisplayName => "Browser Bookmarks";
    public int Priority => 35;
    public string? ActionKeyword => "b";
    public bool SupportsGlobalSearch => true;

    public record BookmarkItem(string Title, string Url, string BrowserName);

    private readonly List<BookmarkItem> _cachedBookmarks = [];
    private volatile BookmarkItem[] _cachedSnapshot = [];
    private DateTime _lastScanTime = DateTime.MinValue;
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    public async Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context)
    {
        var raw = context.RawQuery.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var query = raw;
        var isExplicitKeyword = false;

        if (query.StartsWith("b ", StringComparison.OrdinalIgnoreCase))
        {
            isExplicitKeyword = true;
            query = query[2..].Trim();
        }
        else if (query.StartsWith("bm ", StringComparison.OrdinalIgnoreCase))
        {
            isExplicitKeyword = true;
            query = query[3..].Trim();
        }
        else if (query.Equals("b", StringComparison.OrdinalIgnoreCase) || query.Equals("bm", StringComparison.OrdinalIgnoreCase))
        {
            isExplicitKeyword = true;
            query = string.Empty;
        }

        // Lazy reload bookmarks: initial load waits; periodic refresh runs in background
        if (_cachedSnapshot.Length == 0)
        {
            await RefreshBookmarksAsync();
        }
        else if (DateTime.UtcNow - _lastScanTime > TimeSpan.FromMinutes(10))
        {
            _ = Task.Run(RefreshBookmarksAsync);
        }

        var results = new List<QueryResult>();
        var snapshot = _cachedSnapshot;

        foreach (var bm in snapshot)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            if (isExplicitKeyword && string.IsNullOrEmpty(query))
            {
                results.Add(CreateResult(bm, 80.0));
                if (results.Count >= 50) break;
                continue;
            }

            var titleScore = FuzzyMatcher.Score(bm.Title, query);
            var urlScore = FuzzyMatcher.Score(bm.Url, query);
            var matchScore = Math.Max(titleScore, urlScore * 0.8);

            if (matchScore >= 40.0)
            {
                var finalScore = isExplicitKeyword ? matchScore + 100.0 : matchScore;
                results.Add(CreateResult(bm, finalScore));
            }
        }

        return results.OrderByDescending(r => r.Score).Take(30);
    }

    public async Task RefreshBookmarksAsync()
    {
        if (!await _scanLock.WaitAsync(0))
            return;

        try
        {
            var bookmarks = await Task.Run(() =>
            {
                var list = new List<BookmarkItem>();
                LoadChromiumBookmarks("Chrome", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data"), list);
                LoadChromiumBookmarks("Edge", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data"), list);
                LoadChromiumBookmarks("Brave", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware", "Brave-Browser", "User Data"), list);
                return list;
            });

            _cachedBookmarks.Clear();
            _cachedBookmarks.AddRange(bookmarks);
            _cachedSnapshot = _cachedBookmarks.ToArray();
            _lastScanTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BookmarksPlugin] Refresh error: {ex.Message}");
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private static void LoadChromiumBookmarks(string browserName, string userDataPath, List<BookmarkItem> results)
    {
        if (!Directory.Exists(userDataPath))
            return;

        try
        {
            var profileDirs = Directory.GetDirectories(userDataPath, "Default")
                .Concat(Directory.GetDirectories(userDataPath, "Profile *"));

            foreach (var pDir in profileDirs)
            {
                var bookmarkFile = Path.Combine(pDir, "Bookmarks");
                if (File.Exists(bookmarkFile))
                {
                    try
                    {
                        using var stream = File.OpenRead(bookmarkFile);
                        using var doc = JsonDocument.Parse(stream);
                        if (doc.RootElement.TryGetProperty("roots", out var roots))
                        {
                            foreach (var prop in roots.EnumerateObject())
                            {
                                TraverseChromiumNode(prop.Value, browserName, results);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore read/lock errors
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BookmarksPlugin] Error loading {browserName}: {ex.Message}");
        }
    }

    private static void TraverseChromiumNode(JsonElement node, string browserName, List<BookmarkItem> results)
    {
        if (node.ValueKind != JsonValueKind.Object)
            return;

        if (node.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            if (typeStr == "url")
            {
                var name = node.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                var url = node.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new BookmarkItem(string.IsNullOrWhiteSpace(name) ? url : name, url, browserName));
                }
            }
            else if (typeStr == "folder" && node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                {
                    TraverseChromiumNode(child, browserName, results);
                }
            }
        }
    }

    private static QueryResult CreateResult(BookmarkItem bm, double score)
    {
        return new QueryResult
        {
            Id = $"bm_{bm.Url.GetHashCode()}",
            Title = bm.Title,
            Subtitle = $"[{bm.BrowserName}] {bm.Url}",
            Score = score,
            Type = ResultType.Bookmark,
            IconGlyph = "⭐",
            SourcePluginId = "browser_bookmarks",
            Metadata = new Dictionary<string, string>
            {
                ["Url"] = bm.Url,
                ["Browser"] = bm.BrowserName
            },
            Actions =
            [
                new ResultAction
                {
                    Name = $"Mở {bm.Title}",
                    Description = bm.Url,
                    IconGlyph = "🌐",
                    ShortcutHint = "Enter",
                    SyncAction = () =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = bm.Url,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[BookmarksPlugin] Failed to open bookmark: {ex.Message}");
                        }
                    }
                },
                new ResultAction
                {
                    Name = "Sao chép URL",
                    Description = "Copy liên kết vào Clipboard",
                    IconGlyph = "📋",
                    ShortcutHint = "Ctrl+C",
                    SyncAction = () => { ClipboardHelper.SetText(bm.Url); }
                }
            ]
        };
    }
}
