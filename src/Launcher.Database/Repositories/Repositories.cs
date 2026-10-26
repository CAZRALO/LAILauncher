using Dapper;
using Launcher.Database.Models;

namespace Launcher.Database.Repositories;

public interface IAppRepository
{
    Task<IEnumerable<AppEntity>> GetAllAsync();
    Task<AppEntity?> GetByIdAsync(string id);
    Task UpsertAsync(AppEntity app);
    Task UpsertBatchAsync(IEnumerable<AppEntity> apps);
    Task IncrementLaunchCountAsync(string id);
}

public class AppRepository : IAppRepository
{
    private readonly SqliteConnectionFactory _factory;

    public AppRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<AppEntity>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<AppEntity>("SELECT * FROM Applications ORDER BY launch_count DESC, name ASC;");
    }

    public async Task<AppEntity?> GetByIdAsync(string id)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AppEntity>("SELECT * FROM Applications WHERE id = @Id;", new { Id = id });
    }

    public async Task UpsertAsync(AppEntity app)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO Applications (id, name, path, arguments, icon_path, source, launch_count, last_used, created_at)
            VALUES (@Id, @Name, @Path, @Arguments, @IconPath, @Source, @LaunchCount, @LastUsed, @CreatedAt)
            ON CONFLICT(path) DO UPDATE SET
                name = excluded.name,
                arguments = excluded.arguments,
                icon_path = excluded.icon_path,
                source = excluded.source;
        ";
        await conn.ExecuteAsync(sql, app);
    }

    public async Task UpsertBatchAsync(IEnumerable<AppEntity> apps)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO Applications (id, name, path, arguments, icon_path, source, launch_count, last_used, created_at)
            VALUES (@Id, @Name, @Path, @Arguments, @IconPath, @Source, @LaunchCount, @LastUsed, @CreatedAt)
            ON CONFLICT(path) DO UPDATE SET
                name = excluded.name,
                arguments = excluded.arguments,
                icon_path = excluded.icon_path,
                source = excluded.source;
        ";
        await conn.ExecuteAsync(sql, apps);
    }

    public async Task IncrementLaunchCountAsync(string id)
    {
        using var conn = _factory.CreateConnection();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const string sql = @"
            UPDATE Applications 
            SET launch_count = launch_count + 1, last_used = @Now
            WHERE id = @Id;
        ";
        await conn.ExecuteAsync(sql, new { Id = id, Now = now });
    }
}

public interface IFileRepository
{
    Task<IEnumerable<FileEntity>> GetAllAsync();
    Task<IEnumerable<FileEntity>> GetRecentAsync(int limit = 50);
    Task UpsertBatchAsync(IEnumerable<FileEntity> files);
    Task DeleteBatchAsync(IEnumerable<string> ids);
    Task ClearAllAsync();
}

public class FileRepository : IFileRepository
{
    private readonly SqliteConnectionFactory _factory;

    public FileRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<FileEntity>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<FileEntity>("SELECT id, name, path, extension, size_bytes AS SizeBytes, is_directory AS IsDirectory, modified_time AS ModifiedTime, indexed_at AS IndexedAt FROM Files ORDER BY modified_time DESC;");
    }

    public async Task<IEnumerable<FileEntity>> GetRecentAsync(int limit = 50)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<FileEntity>("SELECT id, name, path, extension, size_bytes AS SizeBytes, is_directory AS IsDirectory, modified_time AS ModifiedTime, indexed_at AS IndexedAt FROM Files ORDER BY modified_time DESC LIMIT @Limit;", new { Limit = limit });
    }

    public async Task UpsertBatchAsync(IEnumerable<FileEntity> files)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            INSERT INTO Files (id, name, path, extension, size_bytes, is_directory, modified_time, indexed_at)
            VALUES (@Id, @Name, @Path, @Extension, @SizeBytes, @IsDirectory, @ModifiedTime, @IndexedAt)
            ON CONFLICT(path) DO UPDATE SET
                name = excluded.name,
                extension = excluded.extension,
                size_bytes = excluded.size_bytes,
                is_directory = excluded.is_directory,
                modified_time = excluded.modified_time,
                indexed_at = excluded.indexed_at;
        ";
        await conn.ExecuteAsync(sql, files);
    }

    public async Task DeleteBatchAsync(IEnumerable<string> ids)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Files WHERE id IN @Ids;", new { Ids = ids });
    }

    public async Task ClearAllAsync()
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Files;");
    }
}

public interface ISearchHistoryRepository
{
    Task AddAsync(string query, string resultId, string resultType);
    Task<int> GetUsageCountAsync(string query, string resultId);
    Task<Dictionary<string, int>> GetUsageCountsAsync(string query, IEnumerable<string> resultIds);
}

public class SearchHistoryRepository : ISearchHistoryRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SearchHistoryRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AddAsync(string query, string resultId, string resultType)
    {
        using var conn = _factory.CreateConnection();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const string sql = @"
            INSERT INTO SearchHistory (query, result_id, result_type, selected_at)
            VALUES (@Query, @ResultId, @ResultType, @Now);
        ";
        await conn.ExecuteAsync(sql, new { Query = query.ToLowerInvariant().Trim(), ResultId = resultId, ResultType = resultType, Now = now });
    }

    public async Task<int> GetUsageCountAsync(string query, string resultId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT COUNT(*) FROM SearchHistory 
            WHERE query = @Query AND result_id = @ResultId;
        ";
        return await conn.ExecuteScalarAsync<int>(sql, new { Query = query.ToLowerInvariant().Trim(), ResultId = resultId });
    }

    public async Task<Dictionary<string, int>> GetUsageCountsAsync(string query, IEnumerable<string> resultIds)
    {
        var idList = resultIds.ToList();
        if (idList.Count == 0)
            return [];

        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT result_id, COUNT(*) as count FROM SearchHistory 
            WHERE query = @Query AND result_id IN @Ids
            GROUP BY result_id;
        ";
        var rows = await conn.QueryAsync<(string result_id, int count)>(sql, new { Query = query.ToLowerInvariant().Trim(), Ids = idList });
        return rows.ToDictionary(r => r.result_id, r => r.count);
    }
}

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}

public class SettingsRepository : ISettingsRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SettingsRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<string?> GetAsync(string key)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<string>("SELECT value FROM Settings WHERE key = @Key;", new { Key = key });
    }

    public async Task SetAsync(string key, string value)
    {
        using var conn = _factory.CreateConnection();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const string sql = @"
            INSERT INTO Settings (key, value, updated_at)
            VALUES (@Key, @Value, @Now)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_at = excluded.updated_at;
        ";
        await conn.ExecuteAsync(sql, new { Key = key, Value = value, Now = now });
    }
}
