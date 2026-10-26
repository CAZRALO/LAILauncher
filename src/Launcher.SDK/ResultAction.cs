namespace Launcher.SDK;

public class ResultAction
{
    public string Name { get; set; } = "Open";
    public string? Description { get; set; }
    public string? IconGlyph { get; set; }
    public Func<Task<bool>>? ActionAsync { get; set; }
    public Action? SyncAction { get; set; }
    public bool IsDangerous { get; set; }
    public string? ConfirmationMessage { get; set; }
    public string? ShortcutHint { get; set; }

    public async Task<bool> ExecuteAsync()
    {
        if (ActionAsync != null)
        {
            return await ActionAsync();
        }

        if (SyncAction != null)
        {
            SyncAction();
            return true;
        }

        return false;
    }
}
