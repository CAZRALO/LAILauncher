using Launcher.Core.Search;
using Launcher.Database.Models;
using Launcher.Database.Repositories;
using Launcher.Indexer;
using Launcher.SDK;
using Xunit;

namespace Launcher.Core.Tests;

public class AppIndexerTests
{
    private class InMemoryAppRepository : IAppRepository
    {
        private readonly List<AppEntity> _apps = [];

        public Task<IEnumerable<AppEntity>> GetAllAsync() => Task.FromResult<IEnumerable<AppEntity>>(_apps.ToList());
        public Task<AppEntity?> GetByIdAsync(string id) => Task.FromResult(_apps.FirstOrDefault(a => a.Id == id));
        public Task UpsertAsync(AppEntity app)
        {
            _apps.RemoveAll(a => a.Path == app.Path);
            _apps.Add(app);
            return Task.CompletedTask;
        }
        public Task UpsertBatchAsync(IEnumerable<AppEntity> apps)
        {
            foreach (var app in apps)
            {
                _apps.RemoveAll(a => a.Path == app.Path);
                _apps.Add(app);
            }
            return Task.CompletedTask;
        }
        public Task IncrementLaunchCountAsync(string id)
        {
            var app = _apps.FirstOrDefault(a => a.Id == id);
            if (app != null) app.LaunchCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task QueryAsync_FindsNotepad_ScrbleInk_CpuInfo_Arc()
    {
        var repo = new InMemoryAppRepository();
        await repo.UpsertBatchAsync([
            new AppEntity
            {
                Id = "app_notepad",
                Name = "Notepad",
                Path = @"shell:AppsFolder\Microsoft.WindowsNotepad_8wekyb3d8bbwe!App",
                Source = "WindowsStore"
            },
            new AppEntity
            {
                Id = "app_scrble",
                Name = "Scrble Ink",
                Path = @"shell:AppsFolder\10511ClaudiaWey.inknotespro_9n0kwq747cbjy!App",
                Source = "WindowsStore"
            },
            new AppEntity
            {
                Id = "app_cpuinfo",
                Name = "CPU-Info",
                Path = @"shell:AppsFolder\11726KamilGurgul.CPU-Info_1tvrk1qs78j88!CPUINFO",
                Source = "WindowsStore"
            },
            new AppEntity
            {
                Id = "app_cpuz",
                Name = "CPU-Z",
                Path = @"C:\Program Files\CPUID\CPU-Z\cpuz.exe",
                Source = "AppsFolder"
            },
            new AppEntity
            {
                Id = "app_arc",
                Name = "Arc",
                Path = @"shell:AppsFolder\TheBrowserCompany.Arc_ttt1ap7aakyb4!Arc",
                Source = "WindowsStore"
            }
        ]);

        var indexer = new AppIndexer(repo);
        await indexer.InitializeAsync();

        // 1. Query "notepad"
        var notepadResults = (await indexer.QueryAsync(new QueryContext("notepad"))).ToList();
        Assert.NotEmpty(notepadResults);
        Assert.Equal("Notepad", notepadResults[0].Title);

        // 2. Query "scrble ink" and "scrble"
        var scrbleInkResults = (await indexer.QueryAsync(new QueryContext("scrble ink"))).ToList();
        Assert.NotEmpty(scrbleInkResults);
        Assert.Equal("Scrble Ink", scrbleInkResults[0].Title);

        var scrbleResults = (await indexer.QueryAsync(new QueryContext("scrble"))).ToList();
        Assert.NotEmpty(scrbleResults);
        Assert.Equal("Scrble Ink", scrbleResults[0].Title);

        // 3. Query "cpu info", "cpuinfo", "cpu"
        var cpuInfoResults = (await indexer.QueryAsync(new QueryContext("cpu info"))).ToList();
        Assert.NotEmpty(cpuInfoResults);
        Assert.Contains(cpuInfoResults, r => r.Title == "CPU-Info");

        var cpuinfoNoSpace = (await indexer.QueryAsync(new QueryContext("cpuinfo"))).ToList();
        Assert.NotEmpty(cpuinfoNoSpace);
        Assert.Contains(cpuinfoNoSpace, r => r.Title == "CPU-Info");

        var cpuQuery = (await indexer.QueryAsync(new QueryContext("cpu"))).ToList();
        Assert.True(cpuQuery.Count >= 2); // Both CPU-Info and CPU-Z

        // 4. Query "arc" and "arc browser"
        var arcResults = (await indexer.QueryAsync(new QueryContext("arc"))).ToList();
        Assert.NotEmpty(arcResults);
        Assert.Equal("Arc", arcResults[0].Title);

        var arcBrowserResults = (await indexer.QueryAsync(new QueryContext("arc browser"))).ToList();
        Assert.NotEmpty(arcBrowserResults);
        Assert.Equal("Arc", arcBrowserResults[0].Title);

        // 5. Query keywords in Vietnamese
        var textEditorQuery = (await indexer.QueryAsync(new QueryContext("soan thao"))).ToList();
        Assert.NotEmpty(textEditorQuery);
        Assert.Contains(textEditorQuery, r => r.Title == "Notepad");

        var drawingQuery = (await indexer.QueryAsync(new QueryContext("ve"))).ToList();
        Assert.NotEmpty(drawingQuery);
        Assert.Contains(drawingQuery, r => r.Title == "Scrble Ink");
    }

    [Fact]
    public void BuiltInSystemApps_ContainsEssentialTools()
    {
        var builtIns = AppScannerHelper.GetBuiltInSystemApps();
        Assert.Contains(builtIns, a => a.Name == "Notepad");
        Assert.Contains(builtIns, a => a.Name == "Calculator");
        Assert.Contains(builtIns, a => a.Name == "Task Manager");
        Assert.Contains(builtIns, a => a.Name == "Paint");
        Assert.Contains(builtIns, a => a.Name == "Windows Settings");
    }

    [Fact]
    public async Task ScanAllAppsAsync_OnRealSystem_DiscoversTargetApps()
    {
        var repo = new InMemoryAppRepository();
        var indexer = new AppIndexer(repo);
        await indexer.ScanAllAppsAsync();

        var apps = (await repo.GetAllAsync()).ToList();
        Assert.NotEmpty(apps);
        Assert.True(apps.Count >= 50, $"Expected at least 50 apps on real system, got {apps.Count}");

        // Check user's specific apps from prompt
        Assert.Contains(apps, a => a.Name.Contains("Notepad", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apps, a => a.Name.Equals("Arc", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apps, a => a.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apps, a => a.Name.Contains("Scrble", StringComparison.OrdinalIgnoreCase));
    }
}
