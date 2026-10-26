using System.Diagnostics;
using System.Runtime.CompilerServices;
using Launcher.AI;
using Launcher.SDK;
using Xunit;

namespace Launcher.Core.Tests;

public class MockAiProvider : IAIProvider
{
    public string ProviderName => "MockOllama";
    public bool IsAvailable => true;
    public string CurrentEndpoint { get; private set; } = "http://localhost:11434";
    public string DefaultModel { get; set; } = "llama3.2:1b";

    public Task<bool> CheckHealthAsync() => Task.FromResult(true);

    public Task<List<string>> GetAvailableModelsAsync() =>
        Task.FromResult(new List<string> { "llama3.2:1b", "qwen2.5:1.5b", "mistral" });

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt, 
        string? model = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "Đây là câu trả lời ";
        yield return "thử nghiệm từ ";
        yield return "Mock AI Provider.";
        await Task.CompletedTask;
    }

    public void UpdateEndpoint(string endpoint)
    {
        CurrentEndpoint = endpoint;
    }
}

public class AIPluginAndIntentClassifierTests
{
    [Fact]
    public void IntentClassifier_ModelLoadsSuccessfully()
    {
        using var classifier = new IntentClassifier();
        Assert.True(classifier.IsModelLoaded, "IntentClassifier model and vocabulary should be loaded successfully.");
    }

    [Theory]
    [InlineData("launch google chrome", IntentType.App)]
    [InlineData("mở visual studio code", IntentType.App)]
    [InlineData("find invoice_2026.pdf", IntentType.File)]
    [InlineData("file: report.docx", IntentType.File)]
    [InlineData("25 * 4 + 10", IntentType.Calculator)]
    [InlineData("tính 100 km to miles", IntentType.Calculator)]
    [InlineData("https://github.com", IntentType.UrlWeb)]
    [InlineData("> ping 8.8.8.8", IntentType.Command)]
    [InlineData("> ipconfig /all", IntentType.Command)]
    [InlineData("ai: giải thích thuật toán quicksort", IntentType.AI)]
    [InlineData("hỏi AI cách deploy docker", IntentType.AI)]
    public void IntentClassifier_PredictsExpectedIntents(string query, IntentType expectedIntent)
    {
        using var classifier = new IntentClassifier();
        var result = classifier.Predict(query);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.True(result.Confidence >= 0.7f, $"Confidence should be >= 0.7, but got {result.Confidence}");
    }

    [Fact]
    public void IntentClassifier_InferenceLatencyIsUnder5ms()
    {
        using var classifier = new IntentClassifier();
        // Warm up
        classifier.Predict("launch chrome");

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            classifier.Predict("ai: giải thích thuật toán dijkstra");
        }
        sw.Stop();

        var averageMs = sw.Elapsed.TotalMilliseconds / 20.0;
        Assert.True(averageMs < 5.0, $"Average inference latency was {averageMs:F2}ms, expected < 5ms.");
    }

    [Fact]
    public async Task AIPlugin_BareKeyword_ReturnsQuickSuggestions()
    {
        var mockProvider = new MockAiProvider();
        using var classifier = new IntentClassifier();
        var plugin = new AIPlugin(mockProvider, classifier);

        var results = (await plugin.QueryAsync(new QueryContext("ai"))).ToList();

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Id == "ai_prompt_guide");
        Assert.Contains(results, r => r.Id == "ai_clipboard_summarize");
        Assert.Contains(results, r => r.Id == "ai_clipboard_explain");
        Assert.Contains(results, r => r.Id == "ai_clipboard_translate");
    }

    [Theory]
    [InlineData("ai summarize", "ai_cmd_summarize")]
    [InlineData("ai: tóm tắt", "ai_cmd_summarize")]
    [InlineData("ai explain", "ai_cmd_explain")]
    [InlineData("ai: giải thích", "ai_cmd_explain")]
    [InlineData("ai translate", "ai_cmd_translate")]
    [InlineData("ai: dịch", "ai_cmd_translate")]
    public async Task AIPlugin_ClipboardActions_AreIdentified(string query, string expectedId)
    {
        var mockProvider = new MockAiProvider();
        using var classifier = new IntentClassifier();
        var plugin = new AIPlugin(mockProvider, classifier);

        var results = (await plugin.QueryAsync(new QueryContext(query))).ToList();

        Assert.NotEmpty(results);
        Assert.Equal(expectedId, results[0].Id);
        Assert.Equal(ResultType.AI, results[0].Type);
    }

    [Fact]
    public async Task AIPlugin_FreeformPrompt_ReturnsStreamingResultItem()
    {
        var mockProvider = new MockAiProvider();
        using var classifier = new IntentClassifier();
        var plugin = new AIPlugin(mockProvider, classifier);

        var query = "ai: viết code giải thuật binary search";
        var results = (await plugin.QueryAsync(new QueryContext(query))).ToList();

        Assert.NotEmpty(results);
        var item = results[0];
        Assert.Equal(ResultType.AI, item.Type);
        Assert.True(item.Metadata.ContainsKey("Prompt"));
        Assert.Contains("binary search", item.Metadata["Prompt"]);
    }

    [Fact]
    public async Task AIPlugin_NonAiQuery_ReturnsEmpty()
    {
        var mockProvider = new MockAiProvider();
        using var classifier = new IntentClassifier();
        var plugin = new AIPlugin(mockProvider, classifier);

        var results = (await plugin.QueryAsync(new QueryContext("notepad"))).ToList();
        Assert.Empty(results);
    }
}
