using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launcher.App.Services;

public static class IconExtractor
{
    private static readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000; // 32x32
    private const uint SHGFI_SMALLICON = 0x000000001; // 16x16
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    #region Win32 IShellItemImageFactory for Modern & Store Apps
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
            [In] int flags,
            [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
        public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
    #endregion

    #region COM ShellLink Definitions for Shortcut Resolution
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkClass
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppszFileName);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }
    #endregion

    /// <summary>
    /// Extracts the clean icon for a file or shortcut path without shortcut overlay arrows.
    /// Results are cached for ultra-fast rendering.
    /// </summary>
    public static ImageSource? GetIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return _cache.GetOrAdd(path, ExtractIconInternal);
    }

    private static ImageSource? ExtractIconInternal(string path)
    {
        try
        {
            // If it is a Windows AppsFolder / UWP / Shell item
            if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("::", StringComparison.OrdinalIgnoreCase))
            {
                var shellImg = ExtractShellItemImage(path);
                if (shellImg != null) return shellImg;
            }

            // If it is a Windows shortcut (.lnk), resolve the actual target & icon location
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var cleanIcon = ExtractCleanShortcutIcon(path);
                if (cleanIcon != null)
                {
                    return cleanIcon;
                }
            }

            // Normal file, folder, or executable
            var img = ExtractFileIcon(path);
            if (img != null) return img;

            // Fallback to Shell Item Image Factory for other items
            return ExtractShellItemImage(path);
        }
        catch
        {
            // Silently fallback
            return null;
        }
    }

    private static ImageSource? ExtractShellItemImage(string path)
    {
        try
        {
            var guid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
            int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, guid, out var factory);
            if (hr == 0 && factory != null)
            {
                int imgHr = factory.GetImage(new SIZE(48, 48), 0, out var hbm);
                if (imgHr == 0 && hbm != IntPtr.Zero)
                {
                    try
                    {
                        var bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                            hbm,
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bitmap.Freeze();
                        return bitmap;
                    }
                    finally
                    {
                        DeleteObject(hbm);
                    }
                }
            }
        }
        catch
        {
            // Silently fallback
        }
        return null;
    }

    /// <summary>
    /// Resolves the shortcut target path or custom icon location to extract the clean icon without shortcut arrows.
    /// </summary>
    private static ImageSource? ExtractCleanShortcutIcon(string lnkPath)
    {
        if (!File.Exists(lnkPath))
            return null;

        try
        {
            var link = (IShellLinkW)new ShellLinkClass();
            var persistFile = (IPersistFile)link;
            
            // STGM_READ = 0
            persistFile.Load(lnkPath, 0);

            // 1. Check if the shortcut specifies a dedicated custom icon file & index
            var iconPathBuf = new StringBuilder(260);
            link.GetIconLocation(iconPathBuf, iconPathBuf.Capacity, out int iconIndex);
            var customIconPath = Environment.ExpandEnvironmentVariables(iconPathBuf.ToString());

            if (!string.IsNullOrWhiteSpace(customIconPath) && File.Exists(customIconPath))
            {
                var img = ExtractFromIconLocation(customIconPath, iconIndex);
                if (img != null) return img;
            }

            // 2. Otherwise get the real executable / document target path
            var targetPathBuf = new StringBuilder(260);
            link.GetPath(targetPathBuf, targetPathBuf.Capacity, out _, 0);
            var targetPath = Environment.ExpandEnvironmentVariables(targetPathBuf.ToString());

            if (!string.IsNullOrWhiteSpace(targetPath) && (File.Exists(targetPath) || Directory.Exists(targetPath)))
            {
                var img = ExtractFileIcon(targetPath);
                if (img != null) return img;
            }
        }
        catch
        {
            // COM resolution failed, continue to fallback
        }

        // Fallback to standard shell icon for the lnk
        return ExtractFileIcon(lnkPath);
    }

    private static ImageSource? ExtractFromIconLocation(string iconPath, int iconIndex)
    {
        try
        {
            IntPtr hLarge = IntPtr.Zero;
            IntPtr hSmall = IntPtr.Zero;
            int count = ExtractIconEx(iconPath, iconIndex, out hLarge, out hSmall, 1);

            IntPtr chosen = hLarge != IntPtr.Zero ? hLarge : hSmall;
            if (chosen != IntPtr.Zero)
            {
                try
                {
                    var bitmap = Imaging.CreateBitmapSourceFromHIcon(chosen, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bitmap.Freeze();
                    return bitmap;
                }
                finally
                {
                    if (hLarge != IntPtr.Zero) DestroyIcon(hLarge);
                    if (hSmall != IntPtr.Zero && hSmall != hLarge) DestroyIcon(hSmall);
                }
            }
        }
        catch
        {
            // Ignore
        }

        return ExtractFileIcon(iconPath);
    }

    private static ImageSource? ExtractFileIcon(string path)
    {
        try
        {
            var shinfo = new SHFILEINFO();
            var flags = SHGFI_ICON | SHGFI_LARGEICON;

            // If file doesn't exist on disk, use extension attributes
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
            }

            var res = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    bitmapSource.Freeze();
                    return bitmapSource;
                }
                finally
                {
                    DestroyIcon(shinfo.hIcon);
                }
            }
        }
        catch
        {
            // Silently fallback
        }

        return null;
    }

    /// <summary>
    /// Pre-caches icons in the background to avoid any frame drops during interactive search.
    /// </summary>
    public static void PreCache(IEnumerable<string> paths)
    {
        Task.Run(() =>
        {
            foreach (var path in paths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    GetIcon(path);
                }
            }
        });
    }
}

