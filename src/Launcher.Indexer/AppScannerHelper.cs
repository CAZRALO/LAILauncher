using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Launcher.Database.Models;
using Microsoft.Win32;

namespace Launcher.Indexer;

public static class AppScannerHelper
{
    #region Win32 KnownFolder Resolution
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr pszPath);

    public static string? ResolveKnownFolderPath(Guid guid)
    {
        int hr = SHGetKnownFolderPath(guid, 0, IntPtr.Zero, out var pPath);
        if (hr == 0 && pPath != IntPtr.Zero)
        {
            try
            {
                return Marshal.PtrToStringUni(pPath);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pPath);
            }
        }
        return null;
    }

    public static string ResolveShellPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return string.Empty;

        // If path begins with a KnownFolder GUID, e.g. {6D809377-6AF0-444B-8957-A3773F02200E}\CPUID\CPU-Z\cpuz.exe
        if (rawPath.StartsWith('{'))
        {
            var closeIdx = rawPath.IndexOf('}');
            if (closeIdx > 1 && Guid.TryParse(rawPath.Substring(1, closeIdx - 1), out var guid))
            {
                var basePath = ResolveKnownFolderPath(guid);
                if (!string.IsNullOrEmpty(basePath))
                {
                    var subPath = rawPath.Substring(closeIdx + 1).TrimStart('\\', '/');
                    var fullPath = Path.Combine(basePath, subPath);
                    if (File.Exists(fullPath) || Directory.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
        }

        // If path is already rooted or starts with shell:
        if (Path.IsPathRooted(rawPath) || rawPath.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return rawPath;
        }

        // Packaged apps (UWP / MSIX / Store AUMID)
        return @"shell:AppsFolder\" + rawPath;
    }
    #endregion

    #region STA Thread Execution for COM Shell
    public static Task<T> RunStaAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }
    #endregion

    /// <summary>
    /// Scans Windows Shell AppsFolder ("shell:AppsFolder") which covers all modern packaged apps
    /// (Notepad, Arc, Scrble Ink, CPU-Info, Calculator, etc.) and registered desktop apps.
    /// </summary>
    public static async Task<List<AppEntity>> ScanAppsFolderAsync()
    {
        return await RunStaAsync(() =>
        {
            var apps = new List<AppEntity>();

            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                    return apps;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic folder = shell.NameSpace("shell:AppsFolder");
                if (folder == null)
                    return apps;

                var items = folder.Items();
                foreach (dynamic item in items)
                {
                    try
                    {
                        string name = item.Name;
                        string rawPath = item.Path;

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rawPath))
                            continue;

                        // Filter out uninstallers and non-app shortcuts
                        if (name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Gỡ cài đặt", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("Modify ", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(" Help", StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith("About ", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var launchPath = ResolveShellPath(rawPath);

                        apps.Add(new AppEntity
                        {
                            Id = "app_" + Guid.NewGuid().ToString("N"),
                            Name = name.Trim(),
                            Path = launchPath,
                            IconPath = launchPath,
                            Source = rawPath.Contains('!') ? "WindowsStore" : "AppsFolder",
                            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        });
                    }
                    catch
                    {
                        // Ignore individual item exceptions
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AppsFolder COM scan error: {ex.Message}");
            }

            return apps;
        });
    }

    /// <summary>
    /// Scans Start Menu directories for .lnk and .url shortcuts.
    /// </summary>
    public static List<AppEntity> ScanStartMenuShortcuts()
    {
        var apps = new List<AppEntity>();

        var userStart = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs");

        var commonStart = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Windows\Start Menu\Programs");

        var directories = new[] { userStart, commonStart };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".url", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrWhiteSpace(name) ||
                        name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Gỡ cài đặt", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    apps.Add(new AppEntity
                    {
                        Id = "lnk_" + Guid.NewGuid().ToString("N"),
                        Name = name.Trim(),
                        Path = file,
                        IconPath = file,
                        Source = "StartMenu",
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Start Menu scan error at {dir}: {ex.Message}");
            }
        }

        return apps;
    }

    /// <summary>
    /// Scans Desktop shortcuts (user desktop and public desktop).
    /// </summary>
    public static List<AppEntity> ScanDesktopShortcuts()
    {
        var apps = new List<AppEntity>();

        var userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        var directories = new[] { userDesktop, commonDesktop };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".url", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrWhiteSpace(name) ||
                        name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    apps.Add(new AppEntity
                    {
                        Id = "desktop_" + Guid.NewGuid().ToString("N"),
                        Name = name.Trim(),
                        Path = file,
                        IconPath = file,
                        Source = "Desktop",
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Desktop scan error at {dir}: {ex.Message}");
            }
        }

        return apps;
    }

    /// <summary>
    /// Scans Windows Registry "App Paths" (HKLM and HKCU) for registered executables like chrome.exe, code.exe, etc.
    /// </summary>
    public static List<AppEntity> ScanRegistryAppPaths()
    {
        var apps = new List<AppEntity>();

        var roots = new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths")
        };

        foreach (var (root, subKeyPath) in roots)
        {
            try
            {
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = key.OpenSubKey(subKeyName);
                        if (appKey == null) continue;

                        var defaultVal = appKey.GetValue(null) as string;
                        if (string.IsNullOrWhiteSpace(defaultVal)) continue;

                        var exePath = defaultVal.Trim('"', ' ');
                        if (File.Exists(exePath))
                        {
                            var cleanName = Path.GetFileNameWithoutExtension(subKeyName);
                            // Try to get FileDescription if available
                            try
                            {
                                var vi = FileVersionInfo.GetVersionInfo(exePath);
                                if (!string.IsNullOrWhiteSpace(vi.FileDescription))
                                {
                                    cleanName = vi.FileDescription;
                                }
                            }
                            catch
                            {
                                // Keep cleanName
                            }

                            apps.Add(new AppEntity
                            {
                                Id = "reg_" + Guid.NewGuid().ToString("N"),
                                Name = cleanName,
                                Path = exePath,
                                IconPath = exePath,
                                Source = "Registry",
                                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                            });
                        }
                    }
                    catch
                    {
                        // Ignore individual subkey error
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Registry App Paths scan error: {ex.Message}");
            }
        }

        return apps;
    }

    /// <summary>
    /// Guaranteed list of built-in Windows Utilities for instant, 100% discoverability.
    /// </summary>
    public static List<AppEntity> GetBuiltInSystemApps()
    {
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        var builtIns = new (string Name, string Path, string? Args)[]
        {
            ("Notepad", Path.Combine(system32, "notepad.exe"), null),
            ("Calculator", Path.Combine(system32, "calc.exe"), null),
            ("Paint", Path.Combine(system32, "mspaint.exe"), null),
            ("Task Manager", Path.Combine(system32, "taskmgr.exe"), null),
            ("Command Prompt", Path.Combine(system32, "cmd.exe"), null),
            ("Windows PowerShell", Path.Combine(system32, @"WindowsPowerShell\v1.0\powershell.exe"), null),
            ("Windows Terminal", "wt.exe", null),
            ("Registry Editor", Path.Combine(windows, "regedit.exe"), null),
            ("Control Panel", Path.Combine(system32, "control.exe"), null),
            ("Device Manager", Path.Combine(system32, "devmgmt.msc"), null),
            ("Disk Management", Path.Combine(system32, "diskmgmt.msc"), null),
            ("Resource Monitor", Path.Combine(system32, "resmon.exe"), null),
            ("System Information", Path.Combine(system32, "msinfo32.exe"), null),
            ("Snipping Tool", Path.Combine(system32, "SnippingTool.exe"), null),
            ("File Explorer", Path.Combine(windows, "explorer.exe"), null),
            ("Windows Settings", "ms-settings:", null)
        };

        var list = new List<AppEntity>();
        foreach (var item in builtIns)
        {
            list.Add(new AppEntity
            {
                Id = "sys_" + item.Name.ToLowerInvariant().Replace(' ', '_'),
                Name = item.Name,
                Path = item.Path,
                Arguments = item.Args,
                IconPath = item.Path,
                Source = "System",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        return list;
    }
}
