using System.Diagnostics;
using System.IO;
using System.Text;

namespace Launcher.Core.Utilities;

public sealed record ShellExecutionResult(
    int ExitCode,
    string Output,
    string Error,
    bool WasCanceled,
    bool DirectoryChanged,
    string WorkingDirectory,
    TimeSpan Duration);

public static class ShellCommandRunner
{
    private const int MaxCapturedChars = 512 * 1024;

    public static string GetDefaultWorkingDirectory()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile) && Directory.Exists(profile))
        {
            return profile;
        }
        return Environment.CurrentDirectory;
    }

    public static string EncodePowerShellCommand(string script)
    {
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    }

    public static bool TryHandleDirectoryChange(
        string command,
        string currentDirectory,
        out string newDirectory,
        out string? errorMessage)
    {
        newDirectory = Directory.Exists(currentDirectory) ? currentDirectory : GetDefaultWorkingDirectory();
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.Trim();
        if (trimmed.IndexOfAny(['&', '|', ';', '>', '<', '\r', '\n']) >= 0)
        {
            return false;
        }

        // Bare drive letter switch (e.g., "D:")
        if (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
        {
            var driveRoot = $"{char.ToUpperInvariant(trimmed[0])}:\\";
            if (Directory.Exists(driveRoot))
            {
                newDirectory = driveRoot;
                return true;
            }

            errorMessage = $"Không tìm thấy thư mục: {driveRoot}";
            return true;
        }

        string? target = null;
        if (trimmed.Equals("cd", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("chdir", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.Equals("cd..", StringComparison.OrdinalIgnoreCase))
        {
            target = "..";
        }
        else if (trimmed.StartsWith("cd ", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("cd\t", StringComparison.OrdinalIgnoreCase))
        {
            target = trimmed[2..].Trim();
        }
        else if (trimmed.StartsWith("chdir ", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("chdir\t", StringComparison.OrdinalIgnoreCase))
        {
            target = trimmed[5..].Trim();
        }
        else
        {
            return false;
        }

        if (target.StartsWith("/d", StringComparison.OrdinalIgnoreCase) &&
            (target.Length == 2 || char.IsWhiteSpace(target[2])))
        {
            target = target[2..].Trim();
        }

        if (target == "/?")
        {
            return false;
        }

        if (string.IsNullOrEmpty(target))
        {
            return true;
        }

        if (target.Length >= 2 && target[0] == '"' && target[^1] == '"')
        {
            target = target[1..^1].Trim();
        }

        if (target == "~")
        {
            target = GetDefaultWorkingDirectory();
        }
        else if (target.StartsWith("~\\", StringComparison.Ordinal) || target.StartsWith("~/", StringComparison.Ordinal))
        {
            target = Path.Combine(GetDefaultWorkingDirectory(), target[2..]);
        }

        target = Environment.ExpandEnvironmentVariables(target);

        try
        {
            var resolved = Path.GetFullPath(target, newDirectory);
            if (Directory.Exists(resolved))
            {
                newDirectory = resolved;
            }
            else
            {
                errorMessage = $"Không tìm thấy thư mục: {resolved}";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Đường dẫn không hợp lệ: {ex.Message}";
        }

        return true;
    }

    public static ProcessStartInfo BuildInlineStartInfo(string command, string workingDirectory, bool usePowerShell = false)
    {
        var validDir = Directory.Exists(workingDirectory) ? workingDirectory : GetDefaultWorkingDirectory();

        var psi = new ProcessStartInfo
        {
            WorkingDirectory = validDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (usePowerShell)
        {
            var script = "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8;\n" + command;
            psi.FileName = "powershell.exe";
            psi.Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {EncodePowerShellCommand(script)}";
        }
        else
        {
            psi.FileName = "cmd.exe";
            psi.Arguments = $"/d /s /c \"chcp 65001>nul & {command}\"";
        }

        return psi;
    }

    public static ProcessStartInfo BuildExternalStartInfo(
        string command,
        string? workingDirectory = null,
        bool runAsAdmin = false,
        bool usePowerShell = false)
    {
        var validDir = !string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory)
            ? workingDirectory
            : GetDefaultWorkingDirectory();

        var psi = new ProcessStartInfo
        {
            WorkingDirectory = validDir,
            UseShellExecute = true
        };

        if (usePowerShell)
        {
            psi.FileName = "powershell.exe";
            psi.Arguments = $"-NoProfile -NoExit -EncodedCommand {EncodePowerShellCommand(command)}";
        }
        else
        {
            psi.FileName = "cmd.exe";
            psi.Arguments = $"/d /s /k \"{command}\"";
        }

        if (runAsAdmin)
        {
            psi.Verb = "runas";
        }

        return psi;
    }

    public static async Task<ShellExecutionResult> ExecuteStreamingAsync(
        string command,
        string workingDirectory,
        bool usePowerShell,
        Action<string, bool>? onOutputLine,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var validDir = Directory.Exists(workingDirectory) ? workingDirectory : GetDefaultWorkingDirectory();

        if (TryHandleDirectoryChange(command, validDir, out var nextDir, out var dirError))
        {
            sw.Stop();
            if (dirError != null)
            {
                onOutputLine?.Invoke(dirError, true);
                return new ShellExecutionResult(1, string.Empty, dirError, false, false, validDir, sw.Elapsed);
            }

            bool changed = !string.Equals(nextDir, validDir, StringComparison.OrdinalIgnoreCase);
            onOutputLine?.Invoke(nextDir, false);
            return new ShellExecutionResult(0, nextDir, string.Empty, false, changed, nextDir, sw.Elapsed);
        }

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using var process = new Process
        {
            StartInfo = BuildInlineStartInfo(command, validDir, usePowerShell)
        };

        process.Start();
        try
        {
            process.StandardInput.Close();
        }
        catch
        {
            // Ignore if stdin was already closed
        }

        using var cancelReg = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may have exited concurrently
            }
        });

        async Task ReadStreamAsync(StreamReader reader, StringBuilder buffer, bool isError)
        {
            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null) break;

                if (buffer.Length < MaxCapturedChars)
                {
                    buffer.AppendLine(line);
                }
                onOutputLine?.Invoke(line, isError);
            }
        }

        var stdoutTask = ReadStreamAsync(process.StandardOutput, outputBuilder, isError: false);
        var stderrTask = ReadStreamAsync(process.StandardError, errorBuilder, isError: true);

        bool wasCanceled = false;
        int exitCode = -1;

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
            exitCode = process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            wasCanceled = true;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore cleanup failure
            }
        }
        finally
        {
            sw.Stop();
        }

        return new ShellExecutionResult(
            ExitCode: exitCode,
            Output: outputBuilder.ToString().TrimEnd('\r', '\n'),
            Error: errorBuilder.ToString().TrimEnd('\r', '\n'),
            WasCanceled: wasCanceled,
            DirectoryChanged: false,
            WorkingDirectory: validDir,
            Duration: sw.Elapsed);
    }
}
