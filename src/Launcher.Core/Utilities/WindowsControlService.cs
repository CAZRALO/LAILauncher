using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
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

public record WifiNetworkInfo(
    string Ssid,
    int SignalQuality,
    bool IsConnected,
    bool HasProfile,
    bool IsSecured,
    string AuthAlgorithm,
    string ProfileName);

public record BluetoothDeviceInfo(
    string Name,
    string Address,
    bool IsConnected,
    bool IsPaired,
    string Category,
    string IconGlyph);

public record BatteryStatusInfo(
    int Percent,
    bool IsCharging,
    bool IsSaverActive);

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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveOverlayScheme(Guid OverlaySchemeGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActualOverlayScheme(out Guid ActualOverlayGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetEffectiveOverlayScheme(out Guid EffectiveOverlayGuid);

    // Native WLAN API (wlanapi.dll)
    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint OpCode, IntPtr pReserved, out uint pdwDataSize, out IntPtr ppData, IntPtr pWlanOpcodeValueType);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, IntPtr pReserved, out IntPtr ppAvailableNetworkList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetProfileList(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pReserved, out IntPtr ppProfileList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanDisconnect(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pReserved);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;
        public int isState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;
        public DOT11_SSID dot11Ssid;
        public int dot11BssType;
        public uint uNumberOfBssids;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bNetworkConnectable;
        public uint wlanNotConnectableReason;
        public uint uNumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public int[] dot11PhyTypes;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bMorePhyTypes;
        public uint wlanSignalQuality;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bSecurityEnabled;
        public int dot11DefaultAuthAlgorithm;
        public int dot11DefaultCipherAlgorithm;
        public uint dwFlags;
        public uint dwReserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_PROFILE_INFO
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;
        public uint dwFlags;
    }

    // Native Bluetooth APIs (BluetoothAPIs.dll)
    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_FIND_RADIO_PARAMS
    {
        public uint dwSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
    {
        public uint dwSize;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnAuthenticated;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnRemembered;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnUnknown;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fReturnConnected;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIssueInquiry;
        public byte cTimeoutMultiplier;
        public IntPtr hRadio;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_INFO
    {
        public uint dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fConnected;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fRemembered;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAuthenticated;
        public SYSTEMTIME stLastSeen;
        public SYSTEMTIME stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextRadio(IntPtr hFind, out IntPtr phRadio);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindRadioClose(IntPtr hFind);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindDeviceClose(IntPtr hFind);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    private static extern uint BluetoothSetServiceState(IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO pbtdi, ref Guid pGuidService, uint dwServiceFlags);

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

    private static bool SafeRunPowercfgSilent(string args)
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
            if (proc.WaitForExit(1500))
            {
                return proc.ExitCode == 0;
            }
        }
        catch { }
        return false;
    }

    private static (int ExitCode, string Output) RunProcessCaptureOutput(string fileName, string args, int timeoutMs = 4000)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };
            proc.Start();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(timeoutMs))
            {
                try { proc.Kill(); } catch { }
                return (-1, string.Empty);
            }

            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            return (proc.ExitCode, string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] RunProcessCaptureOutput failed ({fileName}): {ex.Message}");
            return (-1, string.Empty);
        }
    }

    private static void SafeRunPowerShell(string command)
    {
        try
        {
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] Failed PowerShell command: {ex.Message}");
        }
    }

    private static async Task<(bool Success, string Output)> RunPowerShellEncodedAsync(string script, int timeoutMs = 5500)
    {
        return await Task.Run(() =>
        {
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var (exitCode, output) = RunProcessCaptureOutput(
                "powershell.exe",
                $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}",
                timeoutMs);
            return (exitCode == 0, output.Trim());
        });
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

    public static event Action<bool>? OnDarkModeChanged;

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
        finally
        {
            try
            {
                OnDarkModeChanged?.Invoke(enableDark);
            }
            catch { }
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

    // Standard Power Scheme & Overlay GUIDs
    public const string SchemePowerSaver = "a1841308-3541-4fab-bc81-f71556f20b4a";
    public const string SchemeBalanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string SchemeHighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    private static readonly Guid OverlayBetterBattery = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid OverlayBalanced = Guid.Empty;

    private static string? _cachedPowerSaverGuid;
    private static string? _cachedBalancedGuid;
    private static bool? _cachedBatterySaverState;
    private static DateTime _batteryCacheExpiresUtc = DateTime.MinValue;

    public static event Action<bool>? OnBatterySaverChanged;

    public static void SetPowerScheme(string schemeGuid)
    {
        SafeRunPowercfgSilent($"/setactive {schemeGuid}");
    }

    private static (string SaverGuid, string BalancedGuid) ResolvePowerSchemeGuids()
    {
        if (!string.IsNullOrEmpty(_cachedPowerSaverGuid) && !string.IsNullOrEmpty(_cachedBalancedGuid))
        {
            return (_cachedPowerSaverGuid, _cachedBalancedGuid);
        }

        string saver = SchemePowerSaver;
        string balanced = SchemeBalanced;

        try
        {
            var (_, listOut) = RunProcessCaptureOutput("powercfg.exe", "/list", 1500);
            string? standardSaver = null;
            string? oemSaver = null;
            string? standardBalanced = null;
            string? oemBalanced = null;

            foreach (var rawLine in listOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = rawLine.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;

                var after = rawLine[(idx + 5)..].Trim();
                var spaceIdx = after.IndexOf(' ');
                if (spaceIdx < 32) continue;

                var guidToken = after[..spaceIdx].Trim();
                if (!Guid.TryParse(guidToken, out _)) continue;

                var rest = after[spaceIdx..];
                if (guidToken.Equals(SchemePowerSaver, StringComparison.OrdinalIgnoreCase))
                {
                    standardSaver = guidToken;
                }
                else if (guidToken.Equals(SchemeBalanced, StringComparison.OrdinalIgnoreCase))
                {
                    standardBalanced = guidToken;
                }
                else if (rest.Contains("Energy Saver", StringComparison.OrdinalIgnoreCase) ||
                         rest.Contains("Power saver", StringComparison.OrdinalIgnoreCase) ||
                         rest.Contains("Tiết kiệm", StringComparison.OrdinalIgnoreCase))
                {
                    oemSaver ??= guidToken;
                }
                else if (rest.Contains("Balanced", StringComparison.OrdinalIgnoreCase) ||
                         rest.Contains("Cân bằng", StringComparison.OrdinalIgnoreCase))
                {
                    oemBalanced ??= guidToken;
                }
            }

            saver = standardSaver ?? oemSaver ?? SchemePowerSaver;
            balanced = standardBalanced ?? oemBalanced ?? SchemeBalanced;
        }
        catch { }

        _cachedPowerSaverGuid = saver;
        _cachedBalancedGuid = balanced;
        return (saver, balanced);
    }

    public static bool SetEnergySaver(bool enable)
    {
        bool anyApplied = false;
        try
        {
            // 1. Direct Windows Notification Facility publish (Win 10 & Win 11)
            int payload = enable ? 1 : 2;
            byte[] buffer = BitConverter.GetBytes(payload);
            int res = RtlPublishWnfStateData(WNF_PO_ENERGY_SAVER_OVERRIDE, IntPtr.Zero, buffer, buffer.Length, IntPtr.Zero);
            if (res == 0) anyApplied = true;

            // 2. Native Windows 10/11 Power Overlay API (works without Admin privileges)
            try
            {
                uint overlayRes = PowerSetActiveOverlayScheme(enable ? OverlayBetterBattery : OverlayBalanced);
                if (overlayRes == 0) anyApplied = true;
            }
            catch { }

            // 3. Switch active power scheme & commit threshold via powercfg (also supports OEM Energy Saver plans)
            try
            {
                var (saverGuid, balancedGuid) = ResolvePowerSchemeGuids();
                if (SafeRunPowercfgSilent($"/setactive {(enable ? saverGuid : balancedGuid)}"))
                {
                    anyApplied = true;
                }

                int dcThreshold = enable ? 100 : 20;
                int acThreshold = enable ? 100 : 0;
                SafeRunPowercfgSilent($"/setdcvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD {dcThreshold}");
                SafeRunPowercfgSilent($"/setacvalueindex SCHEME_CURRENT SUB_ENERGYSAVER ESBATTTHRESHOLD {acThreshold}");
                if (SafeRunPowercfgSilent("/setactive SCHEME_CURRENT"))
                {
                    anyApplied = true;
                }
            }
            catch { }

            _cachedBatterySaverState = enable;
            _batteryCacheExpiresUtc = DateTime.UtcNow.AddSeconds(3);
            OnBatterySaverChanged?.Invoke(enable);

            return anyApplied;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] SetEnergySaver error: {ex.Message}");
            return anyApplied;
        }
    }

    public static void EnableBatterySaver() => SetEnergySaver(true);

    public static void DisableBatterySaver() => SetEnergySaver(false);

    public static bool IsBatterySaverActive()
    {
        if (_cachedBatterySaverState.HasValue && DateTime.UtcNow < _batteryCacheExpiresUtc)
        {
            return _cachedBatterySaverState.Value;
        }

        try
        {
            if (GetSystemPowerStatus(out var sps) && sps.SystemStatusFlag == 1)
            {
                return true;
            }
        }
        catch { }

        try
        {
            var status = Registry.GetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Power", "EnergySaverState", null);
            if (status is int stateInt && stateInt == 1)
            {
                return true;
            }
        }
        catch { }

        try
        {
            if (PowerGetActualOverlayScheme(out var actualOverlay) == 0 && actualOverlay == OverlayBetterBattery)
            {
                return true;
            }
        }
        catch { }

        try
        {
            var (saverGuid, _) = ResolvePowerSchemeGuids();
            var (_, output) = RunProcessCaptureOutput("powercfg.exe", "/getactivescheme", 1200);
            if (output.Contains(SchemePowerSaver, StringComparison.OrdinalIgnoreCase) ||
                output.Contains(saverGuid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    public static BatteryStatusInfo GetBatteryStatusInfo()
    {
        int percent = 255;
        bool isCharging = false;
        try
        {
            if (GetSystemPowerStatus(out var sps))
            {
                percent = sps.BatteryLifePercent;
                isCharging = sps.ACLineStatus == 1 || (sps.BatteryFlag & 8) != 0;
            }
        }
        catch { }

        return new BatteryStatusInfo(percent, isCharging, IsBatterySaverActive());
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

    private static bool? _cachedWifiState;
    private static DateTime _wifiCacheExpiresUtc = DateTime.MinValue;
    private static DateTime _lastWifiScanUtc = DateTime.MinValue;

    public static event Action<bool>? OnWifiStateChanged;

    public static void ShowAvailableNetworks()
    {
        // Keep method for compatibility, triggers background scan silently
        TriggerWifiScan();
    }

    public static bool IsWifiEnabled()
    {
        if (_cachedWifiState.HasValue && DateTime.UtcNow < _wifiCacheExpiresUtc)
        {
            return _cachedWifiState.Value;
        }

        IntPtr client = IntPtr.Zero;
        IntPtr ifList = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out client) != 0)
            {
                return true;
            }

            if (WlanEnumInterfaces(client, IntPtr.Zero, out ifList) != 0 || ifList == IntPtr.Zero)
            {
                return false;
            }

            int count = Marshal.ReadInt32(ifList);
            if (count <= 0) return false;

            int infoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
            for (int i = 0; i < count; i++)
            {
                IntPtr ptr = new(ifList.ToInt64() + 8 + (long)i * infoSize);
                var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(ptr);
                Guid ifaceGuid = info.InterfaceGuid;

                // wlan_intf_opcode_radio_state = 8
                if (WlanQueryInterface(client, ref ifaceGuid, 8, IntPtr.Zero, out uint dataSize, out IntPtr radioData, IntPtr.Zero) == 0 &&
                    radioData != IntPtr.Zero)
                {
                    try
                    {
                        if (dataSize >= 16)
                        {
                            int numPhys = Marshal.ReadInt32(radioData, 0);
                            for (int p = 0; p < numPhys && (4 + p * 12 + 12) <= dataSize; p++)
                            {
                                int swState = Marshal.ReadInt32(radioData, 4 + p * 12 + 4);
                                int hwState = Marshal.ReadInt32(radioData, 4 + p * 12 + 8);
                                if (swState == 1 && hwState == 1)
                                {
                                    return true;
                                }
                            }
                            return false;
                        }
                    }
                    finally
                    {
                        WlanFreeMemory(radioData);
                    }
                }

                // Fallback: isState 1..7 indicates active states (not 0=not ready)
                if (info.isState is >= 1 and <= 7)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] IsWifiEnabled error: {ex.Message}");
        }
        finally
        {
            if (ifList != IntPtr.Zero) WlanFreeMemory(ifList);
            if (client != IntPtr.Zero) WlanCloseHandle(client, IntPtr.Zero);
        }

        return false;
    }

    public static string? GetConnectedWifiSsid()
    {
        return GetAvailableWifiNetworks(triggerScan: false).FirstOrDefault(n => n.IsConnected)?.Ssid;
    }

    private static async Task<bool> SetRadioStateViaWinRTAsync(string radioKind, bool enable)
    {
        string targetState = enable ? "On" : "Off";
        string script = $@"
[Windows.Devices.Radios.Radio,Windows.System.Devices,ContentType=WindowsRuntime] | Out-Null
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {{ $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }})[0]
Function Await($WinRtTask, $ResultType) {{
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}}
$radios = Await ([Windows.Devices.Radios.Radio]::GetRadiosAsync()) ([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]])
$changed = $false
foreach ($r in $radios) {{
    if ($r.Kind -eq [Windows.Devices.Radios.RadioKind]::{radioKind}) {{
        $res = Await ($r.SetStateAsync([Windows.Devices.Radios.RadioState]::{targetState})) ([Windows.Devices.Radios.RadioAccessStatus])
        if ($res -eq [Windows.Devices.Radios.RadioAccessStatus]::Allowed) {{ $changed = $true }}
    }}
}}
if ($changed) {{ 'OK' }} else {{ 'FAIL' }}
";
        var (ok, output) = await RunPowerShellEncodedAsync(script, 5500);
        return ok && output.Contains("OK", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> SetWifiEnabledAsync(bool enable)
    {
        bool ok = await SetRadioStateViaWinRTAsync("WiFi", enable);
        if (!ok)
        {
            // Fallback using netsh (if elevated)
            string state = enable ? "enabled" : "disabled";
            var (exit, _) = await Task.Run(() => RunProcessCaptureOutput("netsh.exe", $"interface set interface \"Wi-Fi\" admin={state}", 3000));
            ok = exit == 0;
        }

        _cachedWifiState = enable;
        _wifiCacheExpiresUtc = DateTime.UtcNow.AddSeconds(3);
        OnWifiStateChanged?.Invoke(enable);
        return ok;
    }

    public static void SetWifiEnabled(bool enable)
    {
        _ = SetWifiEnabledAsync(enable);
    }

    public static async Task<bool> ToggleWifiAsync()
    {
        bool targetState = !IsWifiEnabled();
        await SetWifiEnabledAsync(targetState);
        return targetState;
    }

    public static bool ToggleWifi()
    {
        bool targetState = !IsWifiEnabled();
        _ = SetWifiEnabledAsync(targetState);
        return targetState;
    }

    public static void TriggerWifiScan()
    {
        IntPtr client = IntPtr.Zero;
        IntPtr ifList = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out client) != 0) return;
            if (WlanEnumInterfaces(client, IntPtr.Zero, out ifList) != 0 || ifList == IntPtr.Zero) return;

            int count = Marshal.ReadInt32(ifList);
            int infoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
            for (int i = 0; i < count; i++)
            {
                IntPtr ptr = new(ifList.ToInt64() + 8 + (long)i * infoSize);
                var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(ptr);
                Guid ifaceGuid = info.InterfaceGuid;
                WlanScan(client, ref ifaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            _lastWifiScanUtc = DateTime.UtcNow;
        }
        catch { }
        finally
        {
            if (ifList != IntPtr.Zero) WlanFreeMemory(ifList);
            if (client != IntPtr.Zero) WlanCloseHandle(client, IntPtr.Zero);
        }
    }

    public static List<WifiNetworkInfo> GetAvailableWifiNetworks(bool triggerScan = false)
    {
        var merged = new Dictionary<string, WifiNetworkInfo>(StringComparer.OrdinalIgnoreCase);
        var savedProfiles = new List<string>();

        IntPtr client = IntPtr.Zero;
        IntPtr ifList = IntPtr.Zero;
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out client) != 0)
            {
                return [];
            }

            if (WlanEnumInterfaces(client, IntPtr.Zero, out ifList) != 0 || ifList == IntPtr.Zero)
            {
                return [];
            }

            int ifCount = Marshal.ReadInt32(ifList);
            int infoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
            int netSize = Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();
            int profileSize = Marshal.SizeOf<WLAN_PROFILE_INFO>();

            for (int i = 0; i < ifCount; i++)
            {
                IntPtr ptr = new(ifList.ToInt64() + 8 + (long)i * infoSize);
                var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(ptr);
                Guid ifaceGuid = info.InterfaceGuid;

                if (triggerScan || (DateTime.UtcNow - _lastWifiScanUtc).TotalSeconds > 20)
                {
                    WlanScan(client, ref ifaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    _lastWifiScanUtc = DateTime.UtcNow;
                }

                if (WlanGetAvailableNetworkList(client, ref ifaceGuid, 3, IntPtr.Zero, out IntPtr netList) == 0 && netList != IntPtr.Zero)
                {
                    try
                    {
                        int nCount = Marshal.ReadInt32(netList);
                        for (int j = 0; j < nCount; j++)
                        {
                            IntPtr nptr = new(netList.ToInt64() + 8 + (long)j * netSize);
                            var net = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(nptr);

                            if (net.dot11Ssid.uSSIDLength == 0 || net.dot11Ssid.ucSSID == null)
                                continue;

                            int ssidLen = Math.Min((int)net.dot11Ssid.uSSIDLength, net.dot11Ssid.ucSSID.Length);
                            string ssid = Encoding.UTF8.GetString(net.dot11Ssid.ucSSID, 0, ssidLen).Trim('\0');
                            if (string.IsNullOrWhiteSpace(ssid))
                                continue;

                            bool isConnected = (net.dwFlags & 1) != 0;
                            bool hasProfile = (net.dwFlags & 2) != 0 || !string.IsNullOrWhiteSpace(net.strProfileName);
                            int signal = (int)Math.Clamp(net.wlanSignalQuality, 0, 100);
                            string auth = MapWlanAuthAlgorithm(net.dot11DefaultAuthAlgorithm);
                            string profName = net.strProfileName?.Trim() ?? string.Empty;

                            if (merged.TryGetValue(ssid, out var existing))
                            {
                                merged[ssid] = existing with
                                {
                                    SignalQuality = Math.Max(existing.SignalQuality, signal),
                                    IsConnected = existing.IsConnected || isConnected,
                                    HasProfile = existing.HasProfile || hasProfile,
                                    IsSecured = existing.IsSecured || net.bSecurityEnabled,
                                    ProfileName = !string.IsNullOrWhiteSpace(existing.ProfileName) ? existing.ProfileName : profName,
                                    AuthAlgorithm = !string.IsNullOrWhiteSpace(existing.AuthAlgorithm) ? existing.AuthAlgorithm : auth
                                };
                            }
                            else
                            {
                                merged[ssid] = new WifiNetworkInfo(
                                    ssid,
                                    signal,
                                    isConnected,
                                    hasProfile,
                                    net.bSecurityEnabled,
                                    auth,
                                    profName);
                            }
                        }
                    }
                    finally
                    {
                        WlanFreeMemory(netList);
                    }
                }

                if (WlanGetProfileList(client, ref ifaceGuid, IntPtr.Zero, out IntPtr profList) == 0 && profList != IntPtr.Zero)
                {
                    try
                    {
                        int pCount = Marshal.ReadInt32(profList);
                        for (int p = 0; p < pCount; p++)
                        {
                            IntPtr pptr = new(profList.ToInt64() + 8 + (long)p * profileSize);
                            var pinfo = Marshal.PtrToStructure<WLAN_PROFILE_INFO>(pptr);
                            if (!string.IsNullOrWhiteSpace(pinfo.strProfileName))
                            {
                                savedProfiles.Add(pinfo.strProfileName.Trim());
                            }
                        }
                    }
                    finally
                    {
                        WlanFreeMemory(profList);
                    }
                }
            }

            // If only 0-1 visible networks cached (common right after wake/before scan finishes), include recent saved profiles
            if (merged.Count < 2 && savedProfiles.Count > 0)
            {
                int added = 0;
                foreach (var prof in savedProfiles)
                {
                    if (merged.ContainsKey(prof)) continue;
                    merged[prof] = new WifiNetworkInfo(
                        prof,
                        0,
                        IsConnected: false,
                        HasProfile: true,
                        IsSecured: true,
                        AuthAlgorithm: "Đã lưu",
                        ProfileName: prof);
                    added++;
                    if (added >= 8) break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] GetAvailableWifiNetworks error: {ex.Message}");
        }
        finally
        {
            if (ifList != IntPtr.Zero) WlanFreeMemory(ifList);
            if (client != IntPtr.Zero) WlanCloseHandle(client, IntPtr.Zero);
        }

        return merged.Values
            .OrderByDescending(n => n.IsConnected)
            .ThenByDescending(n => n.SignalQuality)
            .ThenByDescending(n => n.HasProfile)
            .ThenBy(n => n.Ssid, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string MapWlanAuthAlgorithm(int auth) => auth switch
    {
        1 => "Open",
        2 => "Shared Key",
        3 => "WPA-Enterprise",
        4 => "WPA-Personal",
        6 => "WPA2-Enterprise",
        7 => "WPA2-Personal",
        9 => "WPA3-Enterprise",
        10 => "WPA3-Personal",
        11 => "OWE",
        _ => "Protected"
    };

    public static (bool Success, string Message) ConnectToWifiNetwork(string ssid, string? password = null, string? profileName = null)
    {
        if (string.IsNullOrWhiteSpace(ssid))
        {
            return (false, "Tên mạng Wi-Fi không hợp lệ.");
        }

        string targetProfile = !string.IsNullOrWhiteSpace(profileName) ? profileName : ssid;

        // If no password supplied and a profile exists (or open network without existing profile)
        if (string.IsNullOrEmpty(password) && !string.IsNullOrWhiteSpace(profileName))
        {
            var (exit, output) = RunProcessCaptureOutput("netsh.exe", $"wlan connect name=\"{targetProfile}\" ssid=\"{ssid}\"", 6000);
            if (exit == 0)
            {
                return (true, $"📶 Đang kết nối tới Wi-Fi \"{ssid}\"...");
            }
            return (false, $"Không thể kết nối tới \"{ssid}\": {output}");
        }

        // Create/add profile XML (Open if password is null/empty, otherwise WPA2-PSK AES)
        string escapedSsid = SecurityElement.Escape(ssid) ?? ssid;
        string profileXml;
        if (string.IsNullOrEmpty(password))
        {
            profileXml = $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
  <name>{escapedSsid}</name>
  <SSIDConfig><SSID><name>{escapedSsid}</name></SSID></SSIDConfig>
  <connectionType>ESS</connectionType>
  <connectionMode>auto</connectionMode>
  <MSM><security><authEncryption><authentication>open</authentication><encryption>none</encryption><useOneX>false</useOneX></authEncryption></security></MSM>
</WLANProfile>";
        }
        else
        {
            string escapedPwd = SecurityElement.Escape(password) ?? password;
            profileXml = $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
  <name>{escapedSsid}</name>
  <SSIDConfig><SSID><name>{escapedSsid}</name></SSID></SSIDConfig>
  <connectionType>ESS</connectionType>
  <connectionMode>auto</connectionMode>
  <MSM><security><authEncryption><authentication>WPA2PSK</authentication><encryption>AES</encryption><useOneX>false</useOneX></authEncryption><sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>{escapedPwd}</keyMaterial></sharedKey></security></MSM>
</WLANProfile>";
        }

        string tempXmlPath = Path.Combine(Path.GetTempPath(), $"lail_wifi_{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(tempXmlPath, profileXml, Encoding.UTF8);
            var (addExit, addOut) = RunProcessCaptureOutput("netsh.exe", $"wlan add profile filename=\"{tempXmlPath}\" user=current", 4000);
            if (addExit != 0)
            {
                return (false, $"Lỗi lưu cấu hình Wi-Fi \"{ssid}\": {addOut}");
            }

            var (connExit, connOut) = RunProcessCaptureOutput("netsh.exe", $"wlan connect name=\"{ssid}\" ssid=\"{ssid}\"", 6000);
            if (connExit == 0)
            {
                return (true, $"📶 Đang kết nối tới Wi-Fi \"{ssid}\"...");
            }
            return (false, $"Không thể kết nối tới \"{ssid}\": {connOut}");
        }
        finally
        {
            try { if (File.Exists(tempXmlPath)) File.Delete(tempXmlPath); } catch { }
        }
    }

    public static (bool Success, string Message) DisconnectWifi()
    {
        IntPtr client = IntPtr.Zero;
        IntPtr ifList = IntPtr.Zero;
        bool disconnected = false;
        try
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out client) == 0 &&
                WlanEnumInterfaces(client, IntPtr.Zero, out ifList) == 0 &&
                ifList != IntPtr.Zero)
            {
                int count = Marshal.ReadInt32(ifList);
                int infoSize = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
                for (int i = 0; i < count; i++)
                {
                    IntPtr ptr = new(ifList.ToInt64() + 8 + (long)i * infoSize);
                    var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(ptr);
                    Guid g = info.InterfaceGuid;
                    if (WlanDisconnect(client, ref g, IntPtr.Zero) == 0)
                    {
                        disconnected = true;
                    }
                }
            }
        }
        catch { }
        finally
        {
            if (ifList != IntPtr.Zero) WlanFreeMemory(ifList);
            if (client != IntPtr.Zero) WlanCloseHandle(client, IntPtr.Zero);
        }

        if (!disconnected)
        {
            var (exit, _) = RunProcessCaptureOutput("netsh.exe", "wlan disconnect", 3000);
            disconnected = exit == 0;
        }

        return disconnected
            ? (true, "📴 Đã ngắt kết nối mạng Wi-Fi hiện tại")
            : (false, "Không thể ngắt kết nối Wi-Fi.");
    }

    public static void OpenWifiSettings() => SafeStartProcess("ms-settings:network-wifi");

    public static void OpenNetworkConnections() => SafeStartProcess("ncpa.cpl");

    #endregion

    #region 6. Bluetooth

    private static bool? _cachedBluetoothState;
    private static DateTime _btCacheExpiresUtc = DateTime.MinValue;
    private static List<BluetoothDeviceInfo> _cachedBleDevices = [];
    private static DateTime _bleCacheExpiresUtc = DateTime.MinValue;
    private static int _bleRefreshInFlight = 0;

    public static event Action<bool>? OnBluetoothStateChanged;

    public static void OpenBluetoothSettings() => SafeStartProcess("ms-settings:bluetooth");

    public static void OpenAddBluetoothDevice() => SafeStartProcess("ms-settings:bluetooth?AddDevice");

    public static bool IsBluetoothEnabled()
    {
        if (_cachedBluetoothState.HasValue && DateTime.UtcNow < _btCacheExpiresUtc)
        {
            return _cachedBluetoothState.Value;
        }

        try
        {
            var rp = new BLUETOOTH_FIND_RADIO_PARAMS
            {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>()
            };
            IntPtr hFind = BluetoothFindFirstRadio(ref rp, out IntPtr hRadio);
            if (hFind != IntPtr.Zero)
            {
                if (hRadio != IntPtr.Zero) CloseHandle(hRadio);
                BluetoothFindRadioClose(hFind);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] IsBluetoothEnabled error: {ex.Message}");
        }

        return false;
    }

    public static async Task<bool> SetBluetoothEnabledAsync(bool enable)
    {
        bool ok = await SetRadioStateViaWinRTAsync("Bluetooth", enable);
        _cachedBluetoothState = enable;
        _btCacheExpiresUtc = DateTime.UtcNow.AddSeconds(3);
        OnBluetoothStateChanged?.Invoke(enable);
        return ok;
    }

    public static void SetBluetoothEnabled(bool enable)
    {
        _ = SetBluetoothEnabledAsync(enable);
    }

    public static async Task<bool> ToggleBluetoothAsync()
    {
        bool targetState = !IsBluetoothEnabled();
        await SetBluetoothEnabledAsync(targetState);
        return targetState;
    }

    public static void ToggleBluetooth()
    {
        _ = ToggleBluetoothAsync();
    }

    public static List<BluetoothDeviceInfo> GetBluetoothDevices(bool forceRefreshBle = false)
    {
        var merged = new Dictionary<string, BluetoothDeviceInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var sp = new BLUETOOTH_DEVICE_SEARCH_PARAMS
            {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
                fReturnAuthenticated = true,
                fReturnRemembered = true,
                fReturnConnected = true,
                fReturnUnknown = false,
                fIssueInquiry = false,
                cTimeoutMultiplier = 0,
                hRadio = IntPtr.Zero
            };
            var info = new BLUETOOTH_DEVICE_INFO
            {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
            };

            IntPtr hFind = BluetoothFindFirstDevice(ref sp, ref info);
            if (hFind != IntPtr.Zero)
            {
                try
                {
                    do
                    {
                        string name = info.szName?.Trim() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string addr = FormatBluetoothAddress(info.Address);
                        var (cat, glyph) = ClassifyBluetoothDevice(info.ulClassofDevice, name);
                        merged[addr] = new BluetoothDeviceInfo(
                            name,
                            addr,
                            info.fConnected,
                            info.fAuthenticated || info.fRemembered,
                            cat,
                            glyph);
                    }
                    while (BluetoothFindNextDevice(hFind, ref info));
                }
                finally
                {
                    BluetoothFindDeviceClose(hFind);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] GetBluetoothDevices classic error: {ex.Message}");
        }

        // Merge BLE paired cache
        var bleSnapshot = _cachedBleDevices;
        foreach (var ble in bleSnapshot)
        {
            bool exists = merged.Values.Any(d =>
                d.Address.Equals(ble.Address, StringComparison.OrdinalIgnoreCase) ||
                d.Name.Equals(ble.Name, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                merged[ble.Address] = ble;
            }
        }

        // Trigger non-blocking BLE cache refresh if stale or requested
        if (forceRefreshBle || DateTime.UtcNow >= _bleCacheExpiresUtc)
        {
            TriggerBackgroundBleRefresh();
        }

        return merged.Values
            .OrderByDescending(d => d.IsConnected)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static async Task RefreshBluetoothDevicesAsync()
    {
        await RefreshBleCacheInternalAsync();
    }

    private static void TriggerBackgroundBleRefresh()
    {
        if (Interlocked.CompareExchange(ref _bleRefreshInFlight, 1, 0) != 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshBleCacheInternalAsync();
            }
            finally
            {
                Interlocked.Exchange(ref _bleRefreshInFlight, 0);
            }
        });
    }

    private static async Task RefreshBleCacheInternalAsync()
    {
        try
        {
            const string script = @"
[Windows.Devices.Enumeration.DeviceInformation,Windows.System.Devices,ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Bluetooth.BluetoothLEDevice,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
Function Await($WinRtTask, $ResultType) {
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}
$sel = [Windows.Devices.Bluetooth.BluetoothLEDevice]::GetDeviceSelectorFromPairingState($true)
$devs = Await ([Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($sel)) ([System.Collections.Generic.IReadOnlyList[Windows.Devices.Enumeration.DeviceInformation]])
foreach ($d in $devs) {
    try {
        $le = Await ([Windows.Devices.Bluetooth.BluetoothLEDevice]::FromIdAsync($d.Id)) ([Windows.Devices.Bluetooth.BluetoothLEDevice])
        if ($le) {
            $addr = $le.BluetoothAddress.ToString('X12')
            $conn = ($le.ConnectionStatus -eq [Windows.Devices.Bluetooth.BluetoothConnectionStatus]::Connected)
            ""$($d.Name)|$addr|$conn""
        }
    } catch {}
}
";
            var (ok, output) = await RunPowerShellEncodedAsync(script, 4500);
            if (!ok) return;

            var list = new List<BluetoothDeviceInfo>();
            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 3) continue;
                string name = parts[0].Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                string rawAddr = parts[1].Trim();
                string addr = rawAddr.Length == 12
                    ? string.Join(":", Enumerable.Range(0, 6).Select(i => rawAddr.Substring(i * 2, 2)))
                    : rawAddr;
                bool isConn = bool.TryParse(parts[2].Trim(), out var c) && c;
                var (_, glyph) = ClassifyBluetoothDevice(0, name);
                list.Add(new BluetoothDeviceInfo(name, addr, isConn, IsPaired: true, "Bluetooth LE", glyph));
            }

            _cachedBleDevices = list;
            _bleCacheExpiresUtc = DateTime.UtcNow.AddSeconds(25);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsControlService] RefreshBleCacheInternalAsync error: {ex.Message}");
        }
    }

    public static async Task<(bool Success, string Message)> ToggleBluetoothDeviceConnectionAsync(BluetoothDeviceInfo device)
    {
        if (string.IsNullOrWhiteSpace(device.Address))
        {
            return (false, $"Không xác định được địa chỉ thiết bị {device.Name}.");
        }

        string cleanHex = device.Address.Replace(":", "").Replace("-", "").Trim();
        if (!ulong.TryParse(cleanHex, System.Globalization.NumberStyles.HexNumber, null, out ulong addrVal))
        {
            return (false, $"Địa chỉ Bluetooth không hợp lệ: {device.Address}");
        }

        bool enable = !device.IsConnected;

        return await Task.Run(() =>
        {
            uint flags = enable ? 1u : 0u;
            Guid[] serviceGuids =
            [
                new("0000110b-0000-1000-8000-00805f9b34fb"), // Audio Sink (A2DP)
                new("0000111e-0000-1000-8000-00805f9b34fb"), // Handsfree (HFP)
                new("00001108-0000-1000-8000-00805f9b34fb"), // Headset (HSP)
                new("00001124-0000-1000-8000-00805f9b34fb")  // HID
            ];

            bool anyChanged = false;
            var rp = new BLUETOOTH_FIND_RADIO_PARAMS
            {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>()
            };

            IntPtr hFind = BluetoothFindFirstRadio(ref rp, out IntPtr hRadio);
            if (hFind == IntPtr.Zero)
            {
                return (false, "Bluetooth hiện đang tắt. Hãy bật Bluetooth trước.");
            }

            try
            {
                do
                {
                    if (hRadio != IntPtr.Zero)
                    {
                        try
                        {
                            var devInfo = new BLUETOOTH_DEVICE_INFO
                            {
                                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
                                Address = addrVal
                            };

                            foreach (var svc in serviceGuids)
                            {
                                var g = svc;
                                uint rc = BluetoothSetServiceState(hRadio, ref devInfo, ref g, flags);
                                if (rc == 0)
                                {
                                    anyChanged = true;
                                }
                            }
                        }
                        finally
                        {
                            CloseHandle(hRadio);
                            hRadio = IntPtr.Zero;
                        }
                    }
                }
                while (BluetoothFindNextRadio(hFind, out hRadio));
            }
            finally
            {
                BluetoothFindRadioClose(hFind);
            }

            if (anyChanged)
            {
                return enable
                    ? (true, $"🟢 Đã kết nối với {device.Name}")
                    : (true, $"⚪ Đã ngắt kết nối {device.Name}");
            }

            return enable
                ? (false, $"Không thể kết nối tới {device.Name}. Hãy đảm bảo thiết bị đang bật và nằm trong phạm vi.")
                : (false, $"Không thể ngắt kết nối {device.Name} từ Windows API.");
        });
    }

    private static string FormatBluetoothAddress(ulong address)
    {
        var hex = address.ToString("X12");
        return $"{hex[..2]}:{hex[2..4]}:{hex[4..6]}:{hex[6..8]}:{hex[8..10]}:{hex[10..12]}";
    }

    private static (string Category, string IconGlyph) ClassifyBluetoothDevice(uint classOfDevice, string name)
    {
        uint majorClass = (classOfDevice >> 8) & 0x1F;
        if (majorClass == 0x04) return ("Âm thanh / Tai nghe", "🎧");
        if (majorClass == 0x05) return ("Chuột / Bàn phím", "🖱️");
        if (majorClass == 0x02) return ("Điện thoại", "📱");
        if (majorClass == 0x01) return ("Máy tính", "💻");

        var lower = name.ToLowerInvariant();
        if (lower.Contains("mouse") || lower.Contains("keyboard") || lower.Contains("sc650") || lower.Contains("aula") || lower.Contains("logi"))
            return ("Ngoại vi", "🖱️");
        if (lower.Contains("buds") || lower.Contains("airpods") || lower.Contains("head") || lower.Contains("ear") || lower.Contains("soundcore") || lower.Contains("hitune") || lower.Contains("monster"))
            return ("Âm thanh", "🎧");
        if (lower.Contains("pad") || lower.Contains("phone") || lower.Contains("realme") || lower.Contains("galaxy") || lower.Contains("iphone"))
            return ("Di động", "📱");

        return ("Thiết bị Bluetooth", "📶");
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
