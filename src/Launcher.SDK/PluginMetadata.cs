namespace Launcher.SDK;

public class PluginMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public bool Enabled { get; set; } = true;
    public List<string> Permissions { get; set; } = [];
}
