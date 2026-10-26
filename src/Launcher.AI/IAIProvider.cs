namespace Launcher.AI;

public enum IntentType
{
    App,
    File,
    Calculator,
    UrlWeb,
    Plugin,
    AI,
    Command
}

public class IntentResult
{
    public IntentType Intent { get; set; }
    public float Confidence { get; set; }
    public string ExtractedEntity { get; set; } = string.Empty;
}

public interface IAIProvider
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    string CurrentEndpoint { get; }
    string DefaultModel { get; set; }
    Task<bool> CheckHealthAsync();
    Task<List<string>> GetAvailableModelsAsync();
    IAsyncEnumerable<string> GenerateStreamAsync(string prompt, string? model = null, CancellationToken cancellationToken = default);
    void UpdateEndpoint(string endpoint);
}
