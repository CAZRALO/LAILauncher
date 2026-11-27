using Launcher.SDK;
using Launcher.Core.Actions;
using Launcher.Database.Repositories;

namespace Launcher.Core.Tests;

public class ActionRunnerTests
{
    [Fact]
    public void CreateProcessAction_Default_HasCorrectProperties()
    {
        var action = ActionRunner.CreateProcessAction(@"C:\Windows\notepad.exe");

        Assert.Equal("Open", action.Name);
        Assert.Equal("Enter", action.ShortcutHint);
        Assert.NotNull(action.SyncAction);
    }

    [Fact]
    public void CreateProcessAction_RunAsAdmin_HasAdminProperties()
    {
        var action = ActionRunner.CreateProcessAction(@"C:\Windows\notepad.exe", runAsAdmin: true);

        Assert.Equal("Run as Administrator", action.Name);
        Assert.Equal("Ctrl+Shift+Enter", action.ShortcutHint);
        Assert.Equal("🛡️", action.IconGlyph);
    }

    [Fact]
    public void CreateOpenFolderAction_HasCorrectProperties()
    {
        var action = ActionRunner.CreateOpenFolderAction(@"C:\Windows\notepad.exe");

        Assert.Equal("Open Containing Folder", action.Name);
        Assert.Equal("Ctrl+Enter", action.ShortcutHint);
        Assert.Equal("📁", action.IconGlyph);
    }

    [Fact]
    public void CreateCopyPathAction_HasCorrectProperties()
    {
        var action = ActionRunner.CreateCopyPathAction(@"C:\Windows\notepad.exe");

        Assert.Equal("Copy Path", action.Name);
        Assert.Equal("Ctrl+C", action.ShortcutHint);
        Assert.Equal("📋", action.IconGlyph);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesTargetAction()
    {
        var executed = false;
        var runner = new ActionRunner();
        var result = new QueryResult
        {
            Title = "Test Item",
            Actions =
            [
                new ResultAction
                {
                    Name = "Test Action",
                    SyncAction = () => { executed = true; }
                }
            ]
        };

        var success = await runner.ExecuteAsync("test", result);

        Assert.True(success);
        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteRunAsAdminAsync_ExecutesAdminActionDirectly()
    {
        var normalExecuted = false;
        var adminExecuted = false;

        var runner = new ActionRunner();
        var result = new QueryResult
        {
            Title = "Command Prompt",
            Actions =
            [
                new ResultAction
                {
                    Name = "Open",
                    SyncAction = () => { normalExecuted = true; }
                },
                new ResultAction
                {
                    Name = "Run as Administrator",
                    SyncAction = () => { adminExecuted = true; }
                }
            ]
        };

        var success = await runner.ExecuteRunAsAdminAsync("cmd", result);

        Assert.True(success);
        Assert.False(normalExecuted);
        Assert.True(adminExecuted);
    }

    [Fact]
    public void CreateCommandAction_Default_UsesCmd()
    {
        var action = ActionRunner.CreateCommandAction("ping 8.8.8.8");

        Assert.Equal("Run Command", action.Name);
        Assert.Equal("Enter", action.ShortcutHint);
        Assert.Equal("⚡", action.IconGlyph);
        Assert.NotNull(action.SyncAction);
    }

    [Fact]
    public void CreateCommandAction_Admin_HasAdminProperties()
    {
        var action = ActionRunner.CreateCommandAction("sfc /scannow", runAsAdmin: true);

        Assert.Contains("Administrator", action.Name);
        Assert.Equal("Ctrl+Shift+Enter", action.ShortcutHint);
        Assert.Equal("🛡️", action.IconGlyph);
    }

    [Fact]
    public void CreateCommandAction_PowerShell_HasPowerShellProperties()
    {
        var action = ActionRunner.CreateCommandAction("Get-Process", usePowerShell: true);

        Assert.Equal("Run in PowerShell", action.Name);
        Assert.Equal("🔷", action.IconGlyph);
    }

    [Fact]
    public void CreateCopyPathAction_ExecutesClipboardCopy()
    {
        string? copied = null;
        Launcher.Core.Utilities.ClipboardHelper.CustomHandler = text => copied = text;

        try
        {
            var action = ActionRunner.CreateCopyPathAction(@"C:\Windows\System32\notepad.exe");
            action.SyncAction?.Invoke();

            Assert.Equal(@"C:\Windows\System32\notepad.exe", copied);
        }
        finally
        {
            Launcher.Core.Utilities.ClipboardHelper.CustomHandler = null;
        }
    }

    [Fact]
    public void CreateWebSearchAction_HasCorrectProperties()
    {
        var action = ActionRunner.CreateWebSearchAction("csharp generics");

        Assert.Equal("Search Google", action.Name);
        Assert.Equal("🌐", action.IconGlyph);
        Assert.Equal("Enter", action.ShortcutHint);
        Assert.NotNull(action.SyncAction);
    }
}
