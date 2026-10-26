using Launcher.SDK;
using Launcher.Database.Repositories;

namespace Launcher.Core.Search;

public class RankingEngine
{
    private readonly ISearchHistoryRepository? _historyRepository;

    public RankingEngine(ISearchHistoryRepository? historyRepository = null)
    {
        _historyRepository = historyRepository;
    }

    public async Task<List<QueryResult>> RankAsync(string query, IEnumerable<QueryResult> results)
    {
        var resultList = results.ToList();
        if (resultList.Count == 0)
            return resultList;

        Dictionary<string, int>? usageMap = null;
        if (_historyRepository != null)
        {
            try
            {
                usageMap = await _historyRepository.GetUsageCountsAsync(query, resultList.Select(r => r.Id));
            }
            catch
            {
                // Isolate repository failure
            }
        }

        foreach (var item in resultList)
        {
            var usageCount = usageMap != null && usageMap.TryGetValue(item.Id, out var count) ? count : 0;
            var usageBonus = Math.Min(30.0, usageCount * 5.0);
            var typeBoost = GetTypeBoost(item.Type);
            var semanticBonus = item.Metadata.TryGetValue("IsSemanticMatch", out var isSem) && isSem == "True" ? 25.0 : 0.0;

            item.Score += typeBoost + usageBonus + semanticBonus;
        }

        var sorted = resultList
            .OrderByDescending(r => r.Score)
            .ThenBy(r => GetTypePriority(r.Type))
            .ThenBy(r => r.Title.Length)
            .ToList();

        // Deduplicate results to prevent duplicate cards (e.g. apps with same name from multiple scan sources)
        var uniqueResults = new List<QueryResult>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in sorted)
        {
            var key = item.Type is ResultType.File or ResultType.Folder
                ? $"{item.Type}:{item.Subtitle ?? item.Title}"
                : $"{item.Type}:{item.Title.Trim()}";

            if (seenKeys.Add(key))
            {
                uniqueResults.Add(item);
            }
        }

        return uniqueResults;
    }

    private static double GetTypeBoost(ResultType type) => type switch
    {
        ResultType.App => 35.0,
        ResultType.SystemCommand => 30.0,
        ResultType.Calculator => 30.0,
        ResultType.AI => 32.0,
        ResultType.Setting => 20.0,
        ResultType.Process => 15.0,
        ResultType.Bookmark => 10.0,
        ResultType.Folder => 5.0,
        ResultType.File => 0.0,
        ResultType.Url => 0.0,
        _ => 0.0
    };

    private static int GetTypePriority(ResultType type) => type switch
    {
        ResultType.App => 0,
        ResultType.SystemCommand => 1,
        ResultType.AI => 2,
        ResultType.Calculator => 3,
        ResultType.Setting => 4,
        ResultType.Process => 5,
        ResultType.Bookmark => 6,
        ResultType.Folder => 7,
        ResultType.File => 8,
        _ => 9
    };
}
