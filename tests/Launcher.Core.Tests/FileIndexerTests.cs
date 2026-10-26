using System.IO;
using Path = System.IO.Path;
using Launcher.SDK;
using Launcher.Database.Models;
using Launcher.Database.Repositories;
using Launcher.Indexer;

namespace Launcher.Core.Tests;

public class MockFileRepository : IFileRepository
{
    private readonly List<FileEntity> _files;

    public MockFileRepository(List<FileEntity> files)
    {
        _files = files;
    }

    public Task<IEnumerable<FileEntity>> GetAllAsync() => Task.FromResult<IEnumerable<FileEntity>>(_files);
    public Task<IEnumerable<FileEntity>> GetRecentAsync(int limit = 50) => Task.FromResult<IEnumerable<FileEntity>>(_files.Take(limit));
    public Task UpsertBatchAsync(IEnumerable<FileEntity> files)
    {
        _files.AddRange(files);
        return Task.CompletedTask;
    }
    public Task DeleteBatchAsync(IEnumerable<string> ids) => Task.CompletedTask;
    public Task ClearAllAsync() => Task.CompletedTask;
}

public class FileIndexerTests
{
    [Fact]
    public async Task QueryAsync_FindsMatchingFilesByFuzzyName()
    {
        var files = new List<FileEntity>
        {
            new() { Id = "1", Name = "FinancialReport2024.xlsx", Path = @"C:\Users\Docs\FinancialReport2024.xlsx", Extension = ".xlsx", SizeBytes = 1024 * 50 },
            new() { Id = "2", Name = "Resume_Software_Engineer.pdf", Path = @"C:\Users\Docs\Resume_Software_Engineer.pdf", Extension = ".pdf", SizeBytes = 1024 * 100 },
            new() { Id = "3", Name = "project_setup.ps1", Path = @"C:\Users\Scripts\project_setup.ps1", Extension = ".ps1", SizeBytes = 500 }
        };

        var repo = new MockFileRepository(files);
        var indexer = new FileIndexer(repo);
        await indexer.InitializeAsync();

        var context = new QueryContext("report");
        var results = (await indexer.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.Equal("FinancialReport2024.xlsx", first.Title);
        Assert.Equal(ResultType.File, first.Type);
        Assert.Contains(first.Actions, a => a.Name == "Open");
        Assert.Contains(first.Actions, a => a.Name == "Open Containing Folder");
        Assert.Contains(first.Actions, a => a.Name == "Copy Path");
    }

    [Fact]
    public async Task QueryAsync_ScriptHasRunAsAdminAction()
    {
        var files = new List<FileEntity>
        {
            new() { Id = "1", Name = "install_service.bat", Path = @"C:\Tools\install_service.bat", Extension = ".bat", SizeBytes = 250 }
        };

        var repo = new MockFileRepository(files);
        var indexer = new FileIndexer(repo);
        await indexer.InitializeAsync();

        var context = new QueryContext("install");
        var results = (await indexer.QueryAsync(context)).ToList();

        Assert.Single(results);
        var item = results[0];
        Assert.Contains(item.Actions, a => a.Name == "Run as Administrator");
    }

    [Fact]
    public async Task QueryAsync_DirectPathQuery_RecognizesDriveRoot()
    {
        var repo = new MockFileRepository([]);
        var indexer = new FileIndexer(repo);
        await indexer.InitializeAsync();

        var context = new QueryContext(@"C:\");
        var results = (await indexer.QueryAsync(context)).ToList();

        // If C:\ exists on the machine, direct navigation returns directories/files
        if (Directory.Exists(@"C:\"))
        {
            Assert.NotEmpty(results);
        }
    }

    [Fact]
    public async Task QueryAsync_FindsMatchingFolderByFuzzyName_ReturnsResultTypeFolder()
    {
        var items = new List<FileEntity>
        {
            new() { Id = "1", Name = "FinancialReport2024.xlsx", Path = @"C:\Users\Docs\FinancialReport2024.xlsx", Extension = ".xlsx", SizeBytes = 50000, IsDirectory = false },
            new() { Id = "2", Name = "Projects", Path = @"C:\Users\Admin\Projects", Extension = null, SizeBytes = 0, IsDirectory = true },
            new() { Id = "3", Name = "Downloads", Path = @"C:\Users\Admin\Downloads", Extension = null, SizeBytes = 0, IsDirectory = true }
        };

        var repo = new MockFileRepository(items);
        var indexer = new FileIndexer(repo);
        await indexer.InitializeAsync();

        var context = new QueryContext("proj");
        var results = (await indexer.QueryAsync(context)).ToList();

        Assert.Single(results);
        var item = results[0];
        Assert.Equal("Projects", item.Title);
        Assert.Equal(ResultType.Folder, item.Type);
        Assert.Equal("📁", item.IconGlyph);
        Assert.Contains(item.Actions, a => a.Name == "Open Folder");
        Assert.Contains(item.Actions, a => a.Name == "Open Terminal as Admin here");
        Assert.Contains(item.Actions, a => a.Name == "Open in Terminal");
        Assert.Contains(item.Actions, a => a.Name == "Open in VS Code");
        Assert.Contains(item.Actions, a => a.Name == "Copy Path");
    }

    [Fact]
    public async Task QueryAsync_PrefixFilter_DirOnlyReturnsFolders()
    {
        var items = new List<FileEntity>
        {
            new() { Id = "1", Name = "react-project.json", Path = @"C:\Users\Admin\react-project.json", Extension = ".json", SizeBytes = 1200, IsDirectory = false },
            new() { Id = "2", Name = "react-project", Path = @"C:\Users\Admin\react-project", Extension = null, SizeBytes = 0, IsDirectory = true }
        };

        var repo = new MockFileRepository(items);
        var indexer = new FileIndexer(repo);
        await indexer.InitializeAsync();

        // 1. Prefix dir:
        var dirResults = (await indexer.QueryAsync(new QueryContext("dir:react"))).ToList();
        Assert.Single(dirResults);
        Assert.Equal(ResultType.Folder, dirResults[0].Type);

        // 2. Prefix folder:
        var folderResults = (await indexer.QueryAsync(new QueryContext("folder:react"))).ToList();
        Assert.Single(folderResults);
        Assert.Equal(ResultType.Folder, folderResults[0].Type);

        // 3. Prefix file:
        var fileResults = (await indexer.QueryAsync(new QueryContext("file:react"))).ToList();
        Assert.Single(fileResults);
        Assert.Equal(ResultType.File, fileResults[0].Type);
    }

    [Fact]
    public async Task FileRepository_Sqlite_InsertAndQueryWorksWithIsDirectory()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        try
        {
            var factory = new Launcher.Database.SqliteConnectionFactory(tempDb);
            var init = new Launcher.Database.DatabaseInitializer(factory);
            init.Initialize();

            var repo = new FileRepository(factory);
            var files = new List<FileEntity>
            {
                new() { Id = "1", Name = "Doc.txt", Path = @"C:\Doc.txt", Extension = ".txt", SizeBytes = 100, IsDirectory = false, ModifiedTime = 1000, IndexedAt = 1000 },
                new() { Id = "2", Name = "Projects", Path = @"C:\Projects", Extension = null, SizeBytes = 0, IsDirectory = true, ModifiedTime = 2000, IndexedAt = 2000 }
            };

            await repo.UpsertBatchAsync(files);
            var all = (await repo.GetAllAsync()).ToList();
            var recent = (await repo.GetRecentAsync(10)).ToList();

            Assert.Equal(2, all.Count);
            Assert.Contains(all, f => f.Name == "Projects" && f.IsDirectory);
            Assert.Contains(all, f => f.Name == "Doc.txt" && !f.IsDirectory);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task DatabaseInitializer_MigratesOldDatabaseWithoutIsDirectoryColumn()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_legacy_{Guid.NewGuid():N}.db");
        try
        {
            var factory = new Launcher.Database.SqliteConnectionFactory(tempDb);
            
            // 1. Simulate legacy database schema without is_directory
            using (var conn = factory.CreateConnection())
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE Files (
                        id TEXT PRIMARY KEY,
                        name TEXT NOT NULL,
                        path TEXT NOT NULL UNIQUE,
                        extension TEXT,
                        size_bytes INTEGER,
                        modified_time INTEGER,
                        indexed_at INTEGER NOT NULL
                    );
                    INSERT INTO Files (id, name, path, extension, size_bytes, modified_time, indexed_at)
                    VALUES ('old-1', 'LegacyFile.pdf', 'C:\LegacyFile.pdf', '.pdf', 5000, 1000, 1000);
                ";
                cmd.ExecuteNonQuery();
            }

            // 2. Run DatabaseInitializer to verify migration upgrades table without error
            var init = new Launcher.Database.DatabaseInitializer(factory);
            init.Initialize();

            // 3. Query using repository
            var repo = new FileRepository(factory);
            var files = (await repo.GetAllAsync()).ToList();

            Assert.Single(files);
            Assert.Equal("LegacyFile.pdf", files[0].Name);
            Assert.False(files[0].IsDirectory);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
