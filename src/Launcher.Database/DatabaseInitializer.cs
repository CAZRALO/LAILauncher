using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

namespace Launcher.Database;

public class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "LAILauncher");
            Directory.CreateDirectory(folder);
            dbPath = Path.Combine(folder, "launcher.db");
        }
        else
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 30
        }.ToString();
    }

    public IDbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        // Fast in-memory pragmas for connection performance
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA synchronous = NORMAL; PRAGMA temp_store = MEMORY; PRAGMA cache_size = -2000;";
        cmd.ExecuteNonQuery();

        return connection;
    }
}

public class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _factory;

    public DatabaseInitializer(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public void Initialize()
    {
        using var conn = _factory.CreateConnection();

        // Enable WAL mode once persistently at database level
        conn.Execute("PRAGMA journal_mode = WAL;");

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS Applications (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                path TEXT NOT NULL UNIQUE,
                arguments TEXT,
                icon_path TEXT,
                source TEXT NOT NULL,
                launch_count INTEGER DEFAULT 0,
                last_used INTEGER,
                created_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_apps_name ON Applications(name);

            CREATE TABLE IF NOT EXISTS Files (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                path TEXT NOT NULL UNIQUE,
                extension TEXT,
                size_bytes INTEGER,
                is_directory INTEGER DEFAULT 0,
                modified_time INTEGER,
                indexed_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_files_name ON Files(name);
            CREATE INDEX IF NOT EXISTS idx_files_extension ON Files(extension);

            CREATE TABLE IF NOT EXISTS SearchHistory (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                query TEXT NOT NULL,
                result_id TEXT NOT NULL,
                result_type TEXT NOT NULL,
                selected_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_history_query ON SearchHistory(query);
            CREATE INDEX IF NOT EXISTS idx_history_query_result ON SearchHistory(query, result_id);

            CREATE TABLE IF NOT EXISTS Plugins (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                version TEXT NOT NULL,
                enabled INTEGER DEFAULT 1,
                permissions TEXT,
                installed_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AIModels (
                id TEXT PRIMARY KEY,
                provider TEXT NOT NULL,
                model_name TEXT NOT NULL,
                endpoint TEXT,
                enabled INTEGER DEFAULT 1,
                metadata TEXT
            );
        ");

        // Migration: Ensure is_directory column exists on pre-existing Files tables
        try
        {
            var columns = conn.Query<string>("SELECT name FROM pragma_table_info('Files');").ToList();
            if (!columns.Contains("is_directory", StringComparer.OrdinalIgnoreCase))
            {
                conn.Execute("ALTER TABLE Files ADD COLUMN is_directory INTEGER DEFAULT 0;");
            }
        }
        catch
        {
            try { conn.Execute("ALTER TABLE Files ADD COLUMN is_directory INTEGER DEFAULT 0;"); } catch { }
        }

        // Create index on is_directory after ensuring column exists
        try
        {
            conn.Execute("CREATE INDEX IF NOT EXISTS idx_files_is_directory ON Files(is_directory);");
        }
        catch
        {
            // Ignore
        }
    }
}
