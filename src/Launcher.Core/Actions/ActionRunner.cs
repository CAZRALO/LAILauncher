using Launcher.SDK;
using Launcher.Database.Repositories;
using Launcher.Core.Utilities;
using System.ComponentModel;
using System.Diagnostics;

namespace Launcher.Core.Actions;

public class ActionRunner
{
    private readonly ISearchHistoryRepository? _historyRepository;
    private readonly IAppRepository? _appRepository;

    public ActionRunner(ISearchHistoryRepository? historyRepository = null, IAppRepository? appRepository = null)
    {
        _historyRepository = historyRepository;
        _appRepository = appRepository;
    }

    public async Task<bool> ExecuteAsync(string query, QueryResult result, ResultAction? action = null)
    {
        var targetAction = action ?? result.PrimaryAction;
        if (targetAction == null)
            return false;

        // Record history for ranking intelligence
        if (_historyRepository != null && !string.IsNullOrWhiteSpace(query))
        {
            try
            {
                await _historyRepository.AddAsync(query, result.Id, result.Type.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActionRunner] Failed to record search history: {ex.Message}");
            }
        }

        if (result.Type == ResultType.App && _appRepository != null)
        {
            try
            {
                await _appRepository.IncrementLaunchCountAsync(result.Id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActionRunner] Failed to increment app launch count: {ex.Message}");
            }
        }

        // Execute action
        try
        {
            return await targetAction.ExecuteAsync();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Debug.WriteLine("[ActionRunner] Elevation canceled by user.");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ActionRunner] Execution failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ExecuteRunAsAdminAsync(string query, QueryResult result)
    {
        // 1. Look for explicit Run as Admin action
        var adminAction = result.Actions.FirstOrDefault(a => a.Name.Contains("Administrator", StringComparison.OrdinalIgnoreCase));
        if (adminAction != null)
        {
            return await ExecuteAsync(query, result, adminAction);
        }

        // 2. If result has a file/app path in Subtitle or Metadata, launch with runas
        var targetPath = result.Metadata.TryGetValue("Path", out var metaPath) ? metaPath : result.Subtitle;
        if (!string.IsNullOrWhiteSpace(targetPath) && (File.Exists(targetPath) || Directory.Exists(targetPath)))
        {
            var dynamicAdminAction = CreateProcessAction(targetPath, null, runAsAdmin: true);
            return await ExecuteAsync(query, result, dynamicAdminAction);
        }

        // 3. Fallback to default action
        return await ExecuteAsync(query, result);
    }

    public static ResultAction CreateProcessAction(string filePath, string? arguments = null, bool runAsAdmin = false, string? customName = null)
    {
        return new ResultAction
        {
            Name = customName ?? (runAsAdmin ? "Run as Administrator" : "Open"),
            Description = runAsAdmin ? "Launch with elevated administrator privileges" : "Open or execute file",
            IconGlyph = runAsAdmin ? "🛡️" : "🚀",
            ShortcutHint = runAsAdmin ? "Ctrl+Shift+Enter" : "Enter",
            SyncAction = () =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };

                if (runAsAdmin)
                {
                    psi.Verb = "runas";
                }

                try
                {
                    Process.Start(psi);
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    Debug.WriteLine("[ActionRunner] Process elevation canceled by user.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ActionRunner] Failed to start process: {ex.Message}");
                }
            }
        };
    }

    public static ResultAction CreateCommandAction(string command, bool runAsAdmin = false, bool usePowerShell = false)
    {
        var shellName = usePowerShell ? "PowerShell" : "CMD";
        var actionName = runAsAdmin 
            ? $"Run as Administrator ({shellName})" 
            : (usePowerShell ? "Run in PowerShell" : "Run Command");

        return new ResultAction
        {
            Name = actionName,
            Description = $"Execute '{command}' in {shellName}" + (runAsAdmin ? " with Administrator privileges" : ""),
            IconGlyph = runAsAdmin ? "🛡️" : (usePowerShell ? "🔷" : "⚡"),
            ShortcutHint = runAsAdmin ? "Ctrl+Shift+Enter" : "Enter",
            SyncAction = () =>
            {
                var psi = ShellCommandRunner.BuildExternalStartInfo(
                    command,
                    workingDirectory: null,
                    runAsAdmin: runAsAdmin,
                    usePowerShell: usePowerShell);

                try
                {
                    Process.Start(psi);
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    Debug.WriteLine("[ActionRunner] Command elevation canceled by user.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to start command: {ex.Message}");
                }
            }
        };
    }

    public static ResultAction CreateWebSearchAction(string query, string? customName = null)
    {
        var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
        return new ResultAction
        {
            Name = customName ?? "Search Google",
            Description = $"Search '{query}' on Google in default web browser",
            IconGlyph = "🌐",
            ShortcutHint = "Enter",
            SyncAction = () =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = searchUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open web search URL: {ex.Message}");
                }
            }
        };
    }

    public static ResultAction CreateOpenFolderAction(string filePath)
    {
        return new ResultAction
        {
            Name = "Open Containing Folder",
            Description = "Reveal this item in Windows File Explorer",
            IconGlyph = "📁",
            ShortcutHint = "Ctrl+Enter",
            SyncAction = () =>
            {
                if (Directory.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                else if (File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{filePath}\"",
                        UseShellExecute = true
                    });
                }
            }
        };
    }

    public static ResultAction CreateCopyPathAction(string filePath)
    {
        return new ResultAction
        {
            Name = "Copy Path",
            Description = "Copy full path to clipboard",
            IconGlyph = "📋",
            ShortcutHint = "Ctrl+C",
            SyncAction = () =>
            {
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    ClipboardHelper.SetText(filePath);
                }
            }
        };
    }
}
