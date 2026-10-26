namespace Launcher.Database.Models;

public class AppEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? IconPath { get; set; }
    public string Source { get; set; } = "StartMenu";
    public int LaunchCount { get; set; }
    public long? LastUsed { get; set; }
    public long CreatedAt { get; set; }
}

public class FileEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public long SizeBytes { get; set; }
    public bool IsDirectory { get; set; }
    public long ModifiedTime { get; set; }
    public long IndexedAt { get; set; }
}

public class SearchHistoryEntity
{
    public long Id { get; set; }
    public string Query { get; set; } = string.Empty;
    public string ResultId { get; set; } = string.Empty;
    public string ResultType { get; set; } = string.Empty;
    public long SelectedAt { get; set; }
}

public class SettingEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public long UpdatedAt { get; set; }
}

public class AIModelEntity
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Metadata { get; set; }
}
