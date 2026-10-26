using Launcher.SDK;
using Launcher.Core.Search;
using Xunit;

namespace Launcher.Core.Tests;

public class QueryPipelineTests
{
    private class TestProvider : ISearchProvider
    {
        public string ProviderId => "test_provider";
        public string DisplayName => "Test Provider";
        public int Priority => 1;

        public Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context)
        {
            var results = new List<QueryResult>();
            if (context.SearchTerm.Contains("calc", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new QueryResult
                {
                    Id = "test_calc_app",
                    Title = "Calculator App",
                    Score = 90.0,
                    Type = ResultType.App
                });
            }
            return Task.FromResult<IEnumerable<QueryResult>>(results);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithMathExpression_ReturnsCalculatorResultFirst()
    {
        var ranking = new RankingEngine();
        var pipeline = new QueryPipeline(ranking);
        pipeline.RegisterProvider(new TestProvider());

        var results = await pipeline.ExecuteAsync("15 * 4");

        Assert.NotEmpty(results);
        Assert.Equal(ResultType.Calculator, results[0].Type);
        Assert.Equal("60", results[0].Title);
    }

    [Fact]
    public async Task ExecuteAsync_WithPercentageMath_ReturnsCorrectResult()
    {
        var ranking = new RankingEngine();
        var pipeline = new QueryPipeline(ranking);

        var results = await pipeline.ExecuteAsync("50 * 20%");

        Assert.NotEmpty(results);
        Assert.Equal(ResultType.Calculator, results[0].Type);
        Assert.Equal("10", results[0].Title);
        Assert.Equal("= 50 * 20%", results[0].Subtitle);
    }

    [Fact]
    public async Task ExecuteAsync_WithProviderQuery_ReturnsProviderResults()
    {
        var ranking = new RankingEngine();
        var pipeline = new QueryPipeline(ranking);
        pipeline.RegisterProvider(new TestProvider());

        var results = await pipeline.ExecuteAsync("calc");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title == "Calculator App");
    }

    [Fact]
    public void QueryResult_IconProperties_RetainAssignedValues()
    {
        var result = new QueryResult
        {
            Id = "app-1",
            Title = "Visual Studio Code",
            IconPath = @"C:\Program Files\VSCode\Code.exe",
            IconGlyph = "💻",
            Type = ResultType.App
        };

        Assert.Equal(@"C:\Program Files\VSCode\Code.exe", result.IconPath);
        Assert.Equal("💻", result.IconGlyph);
        Assert.Equal(ResultType.App, result.Type);
    }

    [Fact]
    public async Task ExecuteAsync_WithCommandPrefix_ReturnsCommandResultFirst()
    {
        var ranking = new RankingEngine();
        var pipeline = new QueryPipeline(ranking);
        pipeline.RegisterProvider(new TestProvider());

        var results = await pipeline.ExecuteAsync("> ping 8.8.8.8");

        Assert.NotEmpty(results);
        var first = results[0];
        Assert.Equal(ResultType.Command, first.Type);
        Assert.Equal("> ping 8.8.8.8", first.Title);
        Assert.Equal("💻", first.IconGlyph);
        Assert.Equal("ping 8.8.8.8", first.Metadata["Command"]);
        Assert.Contains(first.Actions, a => a.Name == "Run Command");
        Assert.Contains(first.Actions, a => a.Name.Contains("Administrator"));
        Assert.Contains(first.Actions, a => a.Name == "Run in PowerShell");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCommandPrefix_ReturnsCommandPrompt()
    {
        var ranking = new RankingEngine();
        var pipeline = new QueryPipeline(ranking);

        var results = await pipeline.ExecuteAsync(">");

        Assert.NotEmpty(results);
        Assert.Equal(ResultType.Command, results[0].Type);
        Assert.Contains("Nhập lệnh", results[0].Title);
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysIncludesGoogleSearchResult()
    {
        var ranking = new RankingEngine();
        var pipeline = new QueryPipeline(ranking);
        pipeline.RegisterProvider(new TestProvider());

        var results = await pipeline.ExecuteAsync("thoi tiet hom nay");

        Assert.NotEmpty(results);
        var googleResult = results.FirstOrDefault(r => r.Type == ResultType.Url && r.Title.Contains("Google"));
        Assert.NotNull(googleResult);
        Assert.Contains("thoi tiet hom nay", googleResult.Title);
        Assert.Contains("https://www.google.com/search?q=thoi%20tiet%20hom%20nay", googleResult.Metadata["Url"]);
        Assert.Equal("🌐", googleResult.IconGlyph);
    }

    [Fact]
    public async Task ExecuteAsync_WithExplicitWebPrefix_RanksGoogleSearchHighest()
    {
        var ranking = new RankingEngine();
        var pipeline = new QueryPipeline(ranking);
        pipeline.RegisterProvider(new TestProvider());

        var results = await pipeline.ExecuteAsync("? learn csharp");

        Assert.NotEmpty(results);
        Assert.Equal(ResultType.Url, results[0].Type);
        Assert.Contains("learn csharp", results[0].Title);
        Assert.True(results[0].Score >= 500.0);
    }

    [Fact]
    public async Task ExecuteAsync_GeminiApp_RanksHigherThanDownloadedFiles()
    {
        var ranking = new RankingEngine();
        var pipeline = new QueryPipeline(ranking);

        var mockAppProvider = new MockResultProvider(
        [
            new QueryResult
            {
                Id = "app_google_gemini",
                Title = "Google Gemini",
                Score = FuzzyMatcher.Score("Google Gemini", "gemini"),
                Type = ResultType.App
            }
        ]);

        var mockFileProvider = new MockResultProvider(
        [
            new QueryResult
            {
                Id = "file_gemini_sh",
                Title = "gemini-code-1784239443558.sh",
                Score = FuzzyMatcher.Score("gemini-code-1784239443558.sh", "gemini"),
                Type = ResultType.File
            },
            new QueryResult
            {
                Id = "file_gemini_png",
                Title = "Gemini_Generated_Image.png",
                Score = FuzzyMatcher.Score("Gemini_Generated_Image.png", "gemini"),
                Type = ResultType.File
            }
        ]);

        pipeline.RegisterProvider(mockAppProvider);
        pipeline.RegisterProvider(mockFileProvider);

        var results = await pipeline.ExecuteAsync("gemini");

        Assert.NotEmpty(results);
        Assert.Equal("Google Gemini", results[0].Title);
        Assert.Equal(ResultType.App, results[0].Type);
    }

    [Theory]
    [InlineData("shut", "Shut Down")]
    [InlineData("slee", "Sleep")]
    [InlineData("hiber", "Hibernate")]
    [InlineData("rest", "Restart Computer")]
    [InlineData("tat may", "Shut Down")]
    [InlineData("tắt máy", "Shut Down")]
    public async Task ExecuteAsync_SystemCommands_PrefixQueries_RankAtTop(string query, string expectedTitle)
    {
        var ranking = new RankingEngine();
        var pipeline = new QueryPipeline(ranking);
        pipeline.RegisterProvider(new Launcher.Core.Plugins.SystemCommandsPlugin());

        // Also add some file noise
        var noiseProvider = new MockResultProvider(
        [
            new QueryResult { Id = "f1", Title = "shuttle_mission.txt", Score = 70.0, Type = ResultType.File },
            new QueryResult { Id = "f2", Title = "sleepless_in_seattle.mp4", Score = 70.0, Type = ResultType.File }
        ]);
        pipeline.RegisterProvider(noiseProvider);

        var results = await pipeline.ExecuteAsync(query);

        Assert.NotEmpty(results);
        Assert.Contains(expectedTitle, results[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ResultType.SystemCommand, results[0].Type);
    }

    [Fact]
    public async Task ExecuteAsync_RealDatabase_GeminiSearch_RanksAppAtTop()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbPath = System.IO.Path.Combine(appData, "LAILauncher", "launcher.db");
        if (!System.IO.File.Exists(dbPath))
            return;

        var factory = new Launcher.Database.SqliteConnectionFactory(dbPath);
        var appRepo = new Launcher.Database.Repositories.AppRepository(factory);
        var fileRepo = new Launcher.Database.Repositories.FileRepository(factory);
        var historyRepo = new Launcher.Database.Repositories.SearchHistoryRepository(factory);

        var appIndexer = new Launcher.Indexer.AppIndexer(appRepo);
        await appIndexer.InitializeAsync();
        var fileIndexer = new Launcher.Indexer.FileIndexer(fileRepo);
        await fileIndexer.InitializeAsync();

        var ranking = new RankingEngine(historyRepo);
        var pipeline = new QueryPipeline(ranking);
        pipeline.RegisterProvider(appIndexer);
        pipeline.RegisterProvider(fileIndexer);

        var results = await pipeline.ExecuteAsync("gemini");

        Assert.NotEmpty(results);
        var topResult = results[0];
        Assert.True(topResult.Title.Contains("Gemini", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ResultType.App, topResult.Type);
    }

    private class MockResultProvider : ISearchProvider
    {
        private readonly List<QueryResult> _results;
        public string ProviderId => "mock_provider";
        public string DisplayName => "Mock";
        public int Priority => 1;

        public MockResultProvider(List<QueryResult> results) => _results = results;

        public Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context) =>
            Task.FromResult<IEnumerable<QueryResult>>(_results);
    }
}
