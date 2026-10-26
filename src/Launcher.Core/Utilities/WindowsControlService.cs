using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Launcher.Core.Utilities;

public enum TaskbarPosition
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}

public enum TaskbarAlignment
{
    Left = 0,
    Center = 1
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class WindowsControlService
{
    #region Win32 P/Invoke & COM Definitions

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("ntdll.dll")]
    private static extern int RtlPublishWnfStateData(
        ulong StateName,
        IntPtr TypeId,
        byte[] Buffer,
        int Length,
        IntPtr ExplicitScope);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag; // 1 = Battery saver is ON, 0 = Battery saver is OFF
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    private const ulong WNF_PO_ENERGY_SAVER_OVERRIDE = 0x41C6013DA3BC3075UL;

    private const int HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private const byte VK_VOLUME_MUTE = 0xAD;
    private const byte VK_VOLUME_DOWN = 0xAE;
    private const byte VK_VOLUME_UP = 0xAF;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        [PreserveSig]
        int GetDevice(string pwstrId, out IMMDevice ppDevice);
        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr pClient);
        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig]
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig]
        int GetState(out int pdwState);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig]
        int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig]
        int GetChannelCount(out uint pnChannelCount);
        [PreserveSig]
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig]
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig]
        int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        [PreserveSig]
        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig]
        int VolumeStepUp(ref Guid pguidEventContext);
        [PreserveSig]
        int VolumeStepDown(ref Guid pguidEventContext);
        [PreserveSig]
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig]
        int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    #endregion

    #region Helper Execution

    public static void SafeStartProcess(string fileName, string? args = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args ?? string.Empty,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] Failed to run {fileName}: {ex.Message}");
        }
    }

    private static void SafeRunPowercfgSilent(string args)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                }
            };
            proc.Start();
            proc.WaitForExit(300);
        }
        catch { }
    }

    private static void SafeRunPowerShell(string command)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] Failed PowerShell command: {ex.Message}");
        }
    }

    private static void BroadcastSettingChange(string section = "ImmersiveColorSet")
    {
        try
        {
            SendMessageTimeout(
                (IntPtr)HWND_BROADCAST,
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                section,
                SMTO_ABORTIFHUNG,
                150,
                out _);
        }
        catch { }
    }

    #endregion

    #region 1. Dark Mode / Light Mode

    public static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int val)
            {
                return val == 0;
            }
        }
        catch { }
        return false;
    }

    public static void SetWindowsDarkMode(bool enableDark)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                int val = enableDark ? 0 : 1;
                key.SetValue("AppsUseLightTheme", val, RegistryValueKind.DWord);
                key.SetValue("SystemUsesLightTheme", val, RegistryValueKind.DWord);
                BroadcastSettingChange("ImmersiveColorSet");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] SetWindowsDarkMode error: {ex.Message}");
        }
    }

    public static bool ToggleWindowsDarkMode()
    {
        bool currentDark = IsWindowsInDarkMode();
        bool newDark = !currentDark;
        SetWindowsDarkMode(newDark);
        return newDark;
    }

    #endregion

    #region 2. Volume Controls

    private static IAudioEndpointVolume? GetAudioEndpointVolume()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            int hr = enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var device);
            if (hr == 0 && device != null)
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                hr = device.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var obj);
                if (hr == 0 && obj is IAudioEndpointVolume vol)
                {
                    return vol;
                }
            }
        }
        catch { }
        return null;
    }

    public static int GetMasterVolume()
    {
        var vol = GetAudioEndpointVolume();
        if (vol != null)
        {
            try
            {
                if (vol.GetMasterVolumeLevelScalar(out float level) == 0)
                {
                    return (int)Math.Round(level * 100);
                }
            }
            catch { }
            finally
            {
                Marshal.ReleaseComObject(vol);
            }
        }
        return 50;
    }

    public static void SetMasterVolume(int volumePercent)
    {
        volumePercent = Math.Clamp(volumePercent, 0, 100);
        var vol = GetAudioEndpointVolume();
        if (vol != null)
        {
            try
            {
                float scalar = volumePercent / 100f;
                var guid = Guid.Empty;
                vol.SetMasterVolumeLevelScalar(scalar, ref guid);
                return;
            }
            catch { }
            finally
            {
                Marshal.ReleaseComObject(vol);
            }
        }

        // Fallback simulation via keybd_event
        int current = GetMasterVolume();
        int diff = volumePercent - current;
        if (diff > 0)
        {
            VolumeUp((diff + 1) / 2);
        }
        else if (diff < 0)
        {
            VolumeDown((-diff + 1) / 2);
        }
    }

    public static void VolumeUp(int steps = 2)
    {
        var vol = GetAudioEndpointVolume();
        if (vol != null)
        {
            try
            {
                if (vol.GetMasterVolumeLevelScalar(out float current) == 0)
                {
                    float next = Math.Clamp(current + (steps * 0.02f), 0f, 1f);
                    var guid = Guid.Empty;
                    vol.SetMasterVolumeLevelScalar(next, ref guid);
                    return;
                }
            }
            catch { }
            finally
            {
                Marshal.ReleaseComObject(vol);
            }
        }

        for (int i = 0; i < steps; i++)
        {
            keybd_event(VK_VOLUME_UP, 0, 0, UIntPtr.Zero);
            keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    public static void VolumeDown(int steps = 2)
    {
        var vol = GetAudioEndpointVolume();
        if (vol != null)
        {
            try
            {
                if (vol.GetMasterVolumeLevelScalar(out float current) == 0)
                {
                    float next = Math.Clamp(current - (steps * 0.02f), 0f, 1f);
                    var guid = Guid.Empty;
                    vol.SetMasterVolumeLevelScalar(next, ref guid);
                    return;
                }
            }
            catch { }
            finally
            {
                Marshal.ReleaseComObject(vol);
            }
        }

        for (int i = 0; i < steps; i++)
        {
            keybd_event(VK_VOLUME_DOWN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    public static bool ToggleMute()
    {
        var vol = GetAudioEndpointVolume();
        if (vol != null)
        {
            try
            {
                if (vol.GetMute(out bool isMuted) == 0)
                {
                    var guid = Guid.Empty;
                    vol.SetMute(!isMuted, ref guid);
                    return !isMuted;
                }
            }
            catch { }
            finally
            {
                Marshal.ReleaseComObject(vol);
            }
        }

        keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
        keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        return false;
    }

    public static bool IsMuted()
    {
        var vol = GetAudioEndpointVolume();
        if (vol != null)
        {
            try
            {
                if (vol.GetMute(out bool isMuted) == 0)
                {
                    return isMuted;
                }
            }
            catch { }
            finally
            {
                Marshal.ReleaseComObject(vol);
            }
        }
        return false;
    }

    public static void OpenVolumeMixer() => SafeStartProcess("sndvol.exe");

    public static void OpenSoundSettings() => SafeStartProcess("ms-settings:sound");

    #endregion

    #region 3. Brightness Controls

    public static int GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["CurrentBrightness"] is byte b) return b;
                if (obj["CurrentBrightness"] is int i) return i;
            }
        }
        catch { }
        return 70; // Fallback estimate
    }

    public static bool SetBrightness(int brightnessPercent)
    {
        brightnessPercent = Math.Clamp(brightnessPercent, 0, 100);
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("WmiSetBrightness", [1, (byte)brightnessPercent]);
                return true;
            }
        }
        catch { }

        // Fallback PowerShell CimInstance
        SafeRunPowerShell($"(Get-CimInstance -Namespace root/WMI -ClassName WmiMonitorBrightnessMethods).WmiSetBrightness(1, {brightnessPercent})");
        return true;
    }

    public static void BrightnessUp(int percent = 10)
    {
        int cur = GetBrightness();
        SetBrightness(cur + percent);
    }

    public static void BrightnessDown(int percent = 10)
    {
        int cur = GetBrightness();
        SetBrightness(cur - percent);
    }

    public static void OpenDisplaySettings() => SafeStartProcess("ms-settings:display");

    public static void OpenNightLightSettings() => SafeStartProcess("ms-settings:nightlight");

    #endregion

    #region 4. Battery Saver

    // Standard Power Scheme GUIDs
    public const string SchemePowerSaver = "a1841308-3541-4fab-bc81-f71556f20b4a";
    public const string SchemeBalanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string SchemeHighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public static void SetPowerScheme(string schemeGuid)
    {
        SafeRunPowercfgSilent($"/setactive {schemeGuid}");
    }

    public static bool SetEnergySaver(bool enable)
    {
        try
        {
            // 1. Direct Windows Notification Facility publish (Works instantaneously on Win 10 & Win 11, AC or Battery)
            // 1 = Enable Energy Saver Override, 2 = Disable Energy Saver Override
            int payload = enable ? 1 : 2;
            byte[] buffer = BitConverter.GetBytes(payload);
            int res = RtlPublishWnfStateData(WNF_PO_ENERGY_SAVER_OVERRIDE, IntPtr.Zero, buffer, buffer.Length, IntPtr.Zero);

            // 2. Set powercfg power scheme & energy saver threshold as silent fallback
            try
            {
                int threshold = enable ? 100 : 20;
                SafeRunPowercfgSilent($"/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD {threshold}");
                SafeRunPowercfgSilent($"/setacvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD {(enable ? 100 : 0)}");
                SafeRunPowercfgSilent($"/setactive {(enable ? SchemePowerSaver : SchemeBalanced)}");
            }
            catch { }

            return res == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] SetEnergySaver error: {ex.Message}");
            return false;
        }
    }

    public static void EnableBatterySaver() => SetEnergySaver(true);

    public static void DisableBatterySaver() => SetEnergySaver(false);

    public static bool IsBatterySaverActive()
    {
        try
        {
            // Check Windows EnergySaverState registry (1 = Enabled, 2 = Disabled)
            var status = Registry.GetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Power", "EnergySaverState", null);
            if (status is int stateInt)
            {
                if (stateInt == 1) return true;
                if (stateInt == 2) return false;
            }
        }
        catch { }

        try
        {
            if (GetSystemPowerStatus(out var sps))
            {
                if (sps.SystemStatusFlag == 1) return true;
            }
        }
        catch { }

        // Fallback: check legacy power scheme
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/getactivescheme",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(300);
            return output.Contains(SchemePowerSaver, StringComparison.OrdinalIgnoreCase);
        }
        catch { }

        return false;
    }

    public static bool ToggleBatterySaver()
    {
        bool currentlyActive = IsBatterySaverActive();
        bool targetState = !currentlyActive;
        SetEnergySaver(targetState);
        return targetState;
    }

    public static void OpenBatterySaverSettings() => SafeStartProcess("ms-settings:batterysaver");

    public static void OpenPowerSleepSettings() => SafeStartProcess("ms-settings:powersleep");

    #endregion

    #region 5. Wi-Fi & Available Networks

    public static void ShowAvailableNetworks()
    {
        // Native Windows Available Networks Flyout
        SafeStartProcess("explorer.exe", "ms-availablenetworks:");
    }

    public static void SetWifiEnabled(bool enable)
    {
        string state = enable ? "enabled" : "disabled";
        SafeStartProcess("netsh.exe", $"interface set interface \"Wi-Fi\" admin={state}");
    }

    public static void OpenWifiSettings() => SafeStartProcess("ms-settings:network-wifi");

    public static void OpenNetworkConnections() => SafeStartProcess("ncpa.cpl");

    #endregion

    #region 6. Bluetooth

    public static void OpenBluetoothSettings() => SafeStartProcess("ms-settings:bluetooth");

    public static void OpenAddBluetoothDevice() => SafeStartProcess("ms-settings:bluetooth?AddDevice");

    public static void ToggleBluetooth()
    {
        // Toggle Bluetooth via Windows Runtime Radios in PowerShell
        string script = @"
[Windows.Devices.Radios.Radio,Windows.System.Devices,ContentType=WindowsRuntime] | Out-Null
$radios = [Windows.Devices.Radios.Radio]::GetRadiosAsync()
$radios.AsTask() | Out-Null
foreach ($r in $radios.GetResults()) {
    if ($r.Kind -eq [Windows.Devices.Radios.RadioKind]::Bluetooth) {
        $newState = if ($r.State -eq [Windows.Devices.Radios.RadioState]::On) { [Windows.Devices.Radios.RadioState]::Off } else { [Windows.Devices.Radios.RadioState]::On }
        $r.SetStateAsync($newState) | Out-Null
    }
}";
        SafeRunPowerShell(script);
    }

    #endregion

    #region 7. Mobile Hotspot

    public static void OpenMobileHotspotSettings() => SafeStartProcess("ms-settings:network-mobilehotspot");

    public static void ToggleMobileHotspot()
    {
        // Direct WinRT Tethering in background without opening Settings!
        string script = @"
[Windows.Networking.Connectivity.NetworkInformation, Windows.Networking.Connectivity, ContentType = WindowsRuntime] | Out-Null
[Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager, Windows.Networking.NetworkOperators, ContentType = WindowsRuntime] | Out-Null
$icp = [Windows.Networking.Connectivity.NetworkInformation]::GetInternetConnectionProfile()
if ($icp) {
    $mgr = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($icp)
    if ($mgr.TetheringOperationalState -eq [Windows.Networking.NetworkOperators.TetheringOperationalState]::On) {
        $mgr.StopTetheringAsync().AsTask().GetAwaiter().GetResult() | Out-Null
    } else {
        $mgr.StartTetheringAsync().AsTask().GetAwaiter().GetResult() | Out-Null
    }
}";
        SafeRunPowerShell(script);
    }

    #endregion

    #region 8. Taskbar Position & Alignment

    public static TaskbarPosition GetTaskbarPosition()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3");
            if (key?.GetValue("Settings") is byte[] bytes && bytes.Length > 12)
            {
                return (TaskbarPosition)bytes[12];
            }
        }
        catch { }
        return TaskbarPosition.Bottom;
    }

    public static void SetTaskbarPosition(TaskbarPosition position)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3", true);
            if (key?.GetValue("Settings") is byte[] bytes && bytes.Length > 12)
            {
                bytes[12] = (byte)position;
                key.SetValue("Settings", bytes, RegistryValueKind.Binary);
                RestartExplorer();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] SetTaskbarPosition error: {ex.Message}");
        }
    }

    public static TaskbarAlignment GetTaskbarAlignment()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            if (key?.GetValue("TaskbarAl") is int val)
            {
                return (TaskbarAlignment)val;
            }
        }
        catch { }
        return TaskbarAlignment.Center;
    }

    public static void SetTaskbarAlignment(TaskbarAlignment alignment, bool restartExplorer = true)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            key?.SetValue("TaskbarAl", (int)alignment, RegistryValueKind.DWord);
            BroadcastSettingChange("TraySettings");
            BroadcastSettingChange("ImmersiveColorSet");
            if (restartExplorer)
            {
                RestartExplorer();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] SetTaskbarAlignment error: {ex.Message}");
        }
    }

    public static TaskbarAlignment ToggleTaskbarAlignment()
    {
        var current = GetTaskbarAlignment();
        var next = current == TaskbarAlignment.Center ? TaskbarAlignment.Left : TaskbarAlignment.Center;
        SetTaskbarAlignment(next, restartExplorer: true);
        return next;
    }

    public static void OpenTaskbarSettings() => SafeStartProcess("ms-settings:taskbar");

    public static void RestartExplorer()
    {
        try
        {
            var proc = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c taskkill /f /im explorer.exe & start explorer.exe",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(proc);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] RestartExplorer error: {ex.Message}");
        }
    }

    #endregion

    #region 9. Windows Accent / Theme Color

    public static readonly (string Name, string Hex)[] WindowsColorPalette =
    [
        ("Electric Blue", "#0078D7"),
        ("Royal Purple", "#744DA9"),
        ("Crimson Red", "#E81123"),
        ("Emerald Green", "#107C41"),
        ("Amber Orange", "#CA5010"),
        ("Vivid Turquoise", "#008272"),
        ("Magenta Pink", "#E3008C"),
        ("Slate Charcoal", "#2D3748")
    ];

    public static string GetWindowsAccentColorHex()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("ColorizationColor") is int val)
            {
                uint u = (uint)val;
                byte r = (byte)((u >> 16) & 0xFF);
                byte g = (byte)((u >> 8) & 0xFF);
                byte b = (byte)(u & 0xFF);
                return $"#{r:X2}{g:X2}{b:X2}";
            }
        }
        catch { }
        return "#0078D7";
    }

    public static void SetWindowsAccentColor(string hexColor)
    {
        try
        {
            var clean = hexColor.Trim().TrimStart('#');
            if (clean.Length == 6 &&
                byte.TryParse(clean[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                byte.TryParse(clean.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                byte.TryParse(clean.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                // ABGR format for DWM AccentColor
                uint abgr = (uint)((0xFF << 24) | (b << 16) | (g << 8) | r);
                // ARGB format for ColorizationColor
                uint argb = (uint)((0xFF << 24) | (r << 16) | (g << 8) | b);

                using var dwmKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\DWM");
                if (dwmKey != null)
                {
                    dwmKey.SetValue("AccentColor", unchecked((int)abgr), RegistryValueKind.DWord);
                    dwmKey.SetValue("ColorizationColor", unchecked((int)argb), RegistryValueKind.DWord);
                }

                using var accentKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
                if (accentKey != null)
                {
                    accentKey.SetValue("AccentColorMenu", unchecked((int)abgr), RegistryValueKind.DWord);
                }

                BroadcastSettingChange("ImmersiveColorSet");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] SetWindowsAccentColor error: {ex.Message}");
        }
    }

    public static void OpenPersonalizationColors() => SafeStartProcess("ms-settings:personalization-colors");

    #endregion

    #region 10. Screenshot / Screen Capture

    public static void LaunchScreenSnipping()
    {
        // Native Windows Screen Clip (Win+Shift+S)
        SafeStartProcess("explorer.exe", "ms-screenclip:");
    }

    public static void CaptureFullScreen()
    {
        try
        {
            // Simulate PrintScreen key to capture screen to clipboard
            keybd_event(0x2C /* VK_SNAPSHOT */, 0, 0, UIntPtr.Zero);
            keybd_event(0x2C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] CaptureFullScreen error: {ex.Message}");
        }
    }

    public static void CaptureAndSaveFullScreen()
    {
        try
        {
            string script = @"
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$dir = [System.IO.Path]::Combine($env:USERPROFILE, 'Pictures', 'Screenshots')
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$file = [System.IO.Path]::Combine($dir, ('Screenshot_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.png'))
$bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
[System.Windows.Forms.Clipboard]::SetImage($bmp)
$g.Dispose()
$bmp.Dispose()
";
            SafeRunPowerShell(script);
        }
        catch
        {
            CaptureFullScreen();
        }
    }

    #endregion
}
