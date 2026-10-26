namespace Launcher.SDK;

public interface ISearchProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    int Priority { get; }
    string? ActionKeyword => null;
    bool SupportsGlobalSearch => true;
    Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context);
}

public interface IPlugin : ISearchProvider, IDisposable
{
    PluginMetadata Metadata { get; }
    Task InitializeAsync();
}

