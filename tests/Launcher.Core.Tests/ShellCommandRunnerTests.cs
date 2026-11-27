using System.IO;
using Launcher.Core.Utilities;
using Xunit;

namespace Launcher.Core.Tests;

public class ShellCommandRunnerTests
{
    [Fact]
    public void BuildInlineStartInfo_Cmd_ConfiguresUtf8AndHiddenWindow()
    {
        var tempDir = Path.GetTempPath();
        var psi = ShellCommandRunner.BuildInlineStartInfo("echo \"hello world\"", tempDir, usePowerShell: false);

        Assert.Equal("cmd.exe", psi.FileName);
        Assert.Equal("/d /s /c \"chcp 65001>nul & echo \"hello world\"\"", psi.Arguments);
        Assert.False(psi.UseShellExecute);
        Assert.True(psi.CreateNoWindow);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.True(psi.RedirectStandardInput);
        Assert.Equal(tempDir, psi.WorkingDirectory);
    }

    [Fact]
    public void BuildInlineStartInfo_PowerShell_UsesEncodedCommand()
    {
        var tempDir = Path.GetTempPath();
        var psi = ShellCommandRunner.BuildInlineStartInfo("Get-Date", tempDir, usePowerShell: true);

        Assert.Equal("powershell.exe", psi.FileName);
        Assert.Contains("-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand ", psi.Arguments);
        Assert.False(psi.UseShellExecute);
        Assert.True(psi.CreateNoWindow);
    }

    [Fact]
    public void BuildExternalStartInfo_CmdAndPowerShell_ConfiguresExpectedArgs()
    {
        var tempDir = Path.GetTempPath();
        var cmdPsi = ShellCommandRunner.BuildExternalStartInfo("ipconfig /all", tempDir, runAsAdmin: true, usePowerShell: false);
        Assert.Equal("cmd.exe", cmdPsi.FileName);
        Assert.Equal("/d /s /k \"ipconfig /all\"", cmdPsi.Arguments);
        Assert.Equal("runas", cmdPsi.Verb);
        Assert.True(cmdPsi.UseShellExecute);
        Assert.Equal(tempDir, cmdPsi.WorkingDirectory);

        var psPsi = ShellCommandRunner.BuildExternalStartInfo("Get-Process", tempDir, runAsAdmin: false, usePowerShell: true);
        Assert.Equal("powershell.exe", psPsi.FileName);
        Assert.Contains("-NoProfile -NoExit -EncodedCommand ", psPsi.Arguments);
        Assert.True(string.IsNullOrEmpty(psPsi.Verb));
        Assert.True(psPsi.UseShellExecute);
    }

    [Fact]
    public void TryHandleDirectoryChange_ValidAndInvalidAndCompoundCommands_BehaveCorrectly()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"lai_cd_test_{Guid.NewGuid():N}");
        var subDir = Path.Combine(baseDir, "sub");
        Directory.CreateDirectory(subDir);

        try
        {
            Assert.True(ShellCommandRunner.TryHandleDirectoryChange("cd sub", baseDir, out var resolvedSub, out var err1));
            Assert.Null(err1);
            Assert.Equal(Path.GetFullPath(subDir), Path.GetFullPath(resolvedSub));

            Assert.True(ShellCommandRunner.TryHandleDirectoryChange("cd ..", subDir, out var resolvedParent, out var err2));
            Assert.Null(err2);
            Assert.Equal(Path.GetFullPath(baseDir), Path.GetFullPath(resolvedParent));

            Assert.True(ShellCommandRunner.TryHandleDirectoryChange("cd /d \"" + subDir + "\"", baseDir, out var resolvedD, out var err3));
            Assert.Null(err3);
            Assert.Equal(Path.GetFullPath(subDir), Path.GetFullPath(resolvedD));

            Assert.True(ShellCommandRunner.TryHandleDirectoryChange("cd does_not_exist_123", baseDir, out var unchanged, out var errMissing));
            Assert.NotNull(errMissing);
            Assert.Equal(baseDir, unchanged);

            // Compound commands must be passed to the real shell instead of being intercepted
            Assert.False(ShellCommandRunner.TryHandleDirectoryChange("cd sub && dir", baseDir, out _, out _));
        }
        finally
        {
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteStreamingAsync_Cmd_StreamsOutputAndReturnsExitCode()
    {
        var lines = new List<string>();
        var result = await ShellCommandRunner.ExecuteStreamingAsync(
            "echo hello_lailauncher",
            Path.GetTempPath(),
            usePowerShell: false,
            (line, _) => { lock (lines) lines.Add(line); });

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.WasCanceled);
        Assert.Contains("hello_lailauncher", result.Output);
        Assert.Contains(lines, l => l.Contains("hello_lailauncher"));
    }

    [Fact]
    public async Task ExecuteStreamingAsync_PowerShell_ExecutesScriptAndReturnsOutput()
    {
        var result = await ShellCommandRunner.ExecuteStreamingAsync(
            "Write-Output 'ps_inline_ok'",
            Path.GetTempPath(),
            usePowerShell: true,
            onOutputLine: null);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.WasCanceled);
        Assert.Contains("ps_inline_ok", result.Output);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_NonZeroExitCode_IsReported()
    {
        var result = await ShellCommandRunner.ExecuteStreamingAsync(
            "exit /b 7",
            Path.GetTempPath(),
            usePowerShell: false,
            onOutputLine: null);

        Assert.Equal(7, result.ExitCode);
        Assert.False(result.WasCanceled);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_Cancellation_StopsLongRunningProcess()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var result = await ShellCommandRunner.ExecuteStreamingAsync(
            "ping -n 30 127.0.0.1",
            Path.GetTempPath(),
            usePowerShell: false,
            onOutputLine: null,
            cts.Token);

        Assert.True(result.WasCanceled);
    }
}
