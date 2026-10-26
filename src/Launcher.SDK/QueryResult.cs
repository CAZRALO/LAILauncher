namespace Launcher.SDK;

public enum ResultType
{
    App,
    File,
    Folder,
    Calculator,
    Url,
    Plugin,
    AI,
    Command,
    Clipboard,
    SystemCommand,
    Bookmark,
    Process,
    Setting
}

public class QueryResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public string? IconGlyph { get; set; }
    public double Score { get; set; }
    public ResultType Type { get; set; } = ResultType.App;
    public string? SourcePluginId { get; set; }
    public string? ShortcutHint { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
    public List<ResultAction> Actions { get; set; } = [];

    public ResultAction? PrimaryAction => Actions.FirstOrDefault();
}
