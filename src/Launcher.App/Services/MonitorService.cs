using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace Launcher.App.Services;

public enum MonitorTargetMode
{
    Cursor = 0,        // Active screen containing mouse cursor (Recommended)
    Primary = 1,       // Windows primary screen
    ActiveWindow = 2,  // Screen containing currently focused/active window
    Specific = 3       // Explicit screen selected by user
}

public enum WindowVerticalPosition
{
    TopCenter = 0,     // ~28% from top of work area (Raycast / Spotlight modern style)
    Center = 1         // Exact vertical center
}

public class MonitorItemInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public Rect Bounds { get; set; }
    public Rect WorkingArea { get; set; }
    public double DpiScaleX { get; set; } = 1.0;
    public double DpiScaleY { get; set; } = 1.0;
    public bool HasCursor { get; set; }

    public string ResolutionText => $"{(int)Bounds.Width} × {(int)Bounds.Height}";
    public string WorkingAreaText => $"{(int)WorkingArea.Width} × {(int)WorkingArea.Height}";
    public string DpiPercentText => $"{(int)Math.Round(DpiScaleX * 100)}%";
    public string StatusBadge => IsPrimary ? "Màn hình chính" : "Màn hình phụ";
}

public static class MonitorService
{
    #region Win32 Interop

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private const uint MONITORINFOF_PRIMARY = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
        public int Width => right - left;
        public int Height => bottom - top;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    #endregion

    /// <summary>
    /// Gets a list of all currently connected physical monitors with DPI-scaled bounds.
    /// </summary>
    public static List<MonitorItemInfo> GetMonitors()
    {
        var list = new List<MonitorItemInfo>();
        POINT cursorPt = default;
        bool hasCursor = GetCursorPos(out cursorPt);

        int index = 1;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
        {
            var mi = new MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf<MONITORINFOEX>();
            if (GetMonitorInfo(hMon, ref mi))
            {
                uint dpiX = 96, dpiY = 96;
                try
                {
                    GetDpiForMonitor(hMon, 0, out dpiX, out dpiY);
                }
                catch
                {
                    dpiX = 96;
                    dpiY = 96;
                }

                if (dpiX == 0) dpiX = 96;
                if (dpiY == 0) dpiY = 96;

                double scaleX = dpiX / 96.0;
                double scaleY = dpiY / 96.0;

                bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;

                // Convert physical pixels to WPF DIPs
                double bLeft = mi.rcMonitor.left / scaleX;
                double bTop = mi.rcMonitor.top / scaleY;
                double bWidth = mi.rcMonitor.Width / scaleX;
                double bHeight = mi.rcMonitor.Height / scaleY;

                double wLeft = mi.rcWork.left / scaleX;
                double wTop = mi.rcWork.top / scaleY;
                double wWidth = mi.rcWork.Width / scaleX;
                double wHeight = mi.rcWork.Height / scaleY;

                bool cursorInside = hasCursor &&
                    cursorPt.x >= mi.rcMonitor.left && cursorPt.x < mi.rcMonitor.right &&
                    cursorPt.y >= mi.rcMonitor.top && cursorPt.y < mi.rcMonitor.bottom;

                list.Add(new MonitorItemInfo
                {
                    Index = index,
                    DeviceName = mi.szDevice ?? $"\\\\.\\DISPLAY{index}",
                    DisplayName = isPrimary ? $"Màn hình {index} (Chính)" : $"Màn hình {index}",
                    IsPrimary = isPrimary,
                    Bounds = new Rect(bLeft, bTop, bWidth, bHeight),
                    WorkingArea = new Rect(wLeft, wTop, wWidth, wHeight),
                    DpiScaleX = scaleX,
                    DpiScaleY = scaleY,
                    HasCursor = cursorInside
                });

                index++;
            }
            return true;
        }, IntPtr.Zero);

        if (list.Count == 0)
        {
            list.Add(new MonitorItemInfo
            {
                Index = 1,
                DeviceName = "\\\\.\\DISPLAY1",
                DisplayName = "Màn hình chính",
                IsPrimary = true,
                Bounds = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                WorkingArea = new Rect(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height),
                HasCursor = true
            });
        }

        return list;
    }

    /// <summary>
    /// Finds the target monitor according to the specified mode and configuration.
    /// </summary>
    public static MonitorItemInfo GetTargetMonitor(MonitorTargetMode mode, string? specificDeviceName)
    {
        var monitors = GetMonitors();
        if (monitors.Count == 0)
        {
            return new MonitorItemInfo
            {
                Index = 1,
                DeviceName = "\\\\.\\DISPLAY1",
                DisplayName = "Màn hình chính",
                IsPrimary = true,
                Bounds = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                WorkingArea = new Rect(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height),
                HasCursor = true
            };
        }

        switch (mode)
        {
            case MonitorTargetMode.Cursor:
                var cursorMon = monitors.FirstOrDefault(m => m.HasCursor);
                if (cursorMon != null) return cursorMon;

                // Fallback check with MonitorFromPoint
                if (GetCursorPos(out var pt))
                {
                    var hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                    var found = FindMonitorByHandle(monitors, hMon);
                    if (found != null) return found;
                }
                break;

            case MonitorTargetMode.ActiveWindow:
                var foreHwnd = GetForegroundWindow();
                if (foreHwnd != IntPtr.Zero)
                {
                    var hMon = MonitorFromWindow(foreHwnd, MONITOR_DEFAULTTONEAREST);
                    var found = FindMonitorByHandle(monitors, hMon);
                    if (found != null) return found;
                }
                break;

            case MonitorTargetMode.Specific:
                if (!string.IsNullOrWhiteSpace(specificDeviceName))
                {
                    var match = monitors.FirstOrDefault(m =>
                        string.Equals(m.DeviceName, specificDeviceName, StringComparison.OrdinalIgnoreCase) ||
                        m.Index.ToString() == specificDeviceName);
                    if (match != null) return match;
                }
                break;

            case MonitorTargetMode.Primary:
            default:
                break;
        }

        // Default / Safe Fallback: Primary monitor
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    private static MonitorItemInfo? FindMonitorByHandle(List<MonitorItemInfo> monitors, IntPtr hMon)
    {
        if (hMon == IntPtr.Zero) return null;
        var mi = new MONITORINFOEX();
        mi.cbSize = Marshal.SizeOf<MONITORINFOEX>();
        if (GetMonitorInfo(hMon, ref mi) && !string.IsNullOrEmpty(mi.szDevice))
        {
            return monitors.FirstOrDefault(m => string.Equals(m.DeviceName, mi.szDevice, StringComparison.OrdinalIgnoreCase));
        }
        return null;
    }

    /// <summary>
    /// Positions the specified WPF Window smoothly onto the target monitor.
    /// If hasCustomPosition is true and coordinates are within visible screen bounds, uses them.
    /// Otherwise centers smoothly on the active/target monitor.
    /// </summary>
    public static void PositionWindow(
        Window window,
        MonitorTargetMode mode = MonitorTargetMode.Cursor,
        string? specificDeviceName = null,
        WindowVerticalPosition verticalPos = WindowVerticalPosition.TopCenter,
        bool hasCustomPosition = false,
        double customLeft = 0,
        double customTop = 0)
    {
        try
        {
            double winWidth = window.ActualWidth > 0 ? window.ActualWidth : (double.IsNaN(window.Width) ? 760 : window.Width);
            double winHeight = window.ActualHeight > 0 ? window.ActualHeight : (double.IsNaN(window.Height) ? 530 : window.Height);

            if (hasCustomPosition)
            {
                // Validate that custom position is still within visible virtual desktop space
                double vLeft = SystemParameters.VirtualScreenLeft;
                double vTop = SystemParameters.VirtualScreenTop;
                double vWidth = SystemParameters.VirtualScreenWidth;
                double vHeight = SystemParameters.VirtualScreenHeight;

                if (customLeft + winWidth >= vLeft + 80 &&
                    customLeft <= vLeft + vWidth - 80 &&
                    customTop >= vTop &&
                    customTop <= vTop + vHeight - 80)
                {
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = customLeft;
                    window.Top = customTop;
                    return;
                }
            }

            var target = GetTargetMonitor(mode, specificDeviceName);
            var workArea = target.WorkingArea;

            // Center horizontally on target monitor
            double targetLeft = workArea.Left + (workArea.Width - winWidth) / 2.0;

            // Vertical positioning
            double targetTop;
            if (verticalPos == WindowVerticalPosition.TopCenter)
            {
                // Top-Center style (around ~25%-28% of available vertical space)
                double availableY = Math.Max(0, workArea.Height - winHeight);
                targetTop = workArea.Top + Math.Max(24.0, availableY * 0.28);
            }
            else
            {
                // True center
                targetTop = workArea.Top + (workArea.Height - winHeight) / 2.0;
            }

            // Ensure window doesn't overflow work area boundaries
            if (targetLeft < workArea.Left) targetLeft = workArea.Left;
            if (targetTop < workArea.Top) targetTop = workArea.Top;

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = targetLeft;
            window.Top = targetTop;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MonitorService] Failed to position window: {ex.Message}");
        }
    }
}
