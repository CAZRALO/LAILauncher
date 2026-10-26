using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Launcher.App.Services;

public enum ThemeMode
{
    Dark,
    Light,
    System,
    Glass,
    Cyberpunk,
    Nord,
    Dracula,
    TokyoNight,
    Emerald,
    Sunset,
    Linux,
    Ubuntu,
    Arch,
    Aurora
}

public enum OsStyle
{
    Windows,
    Linux
}

public static class ThemeManager
{
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetColorizationColor")]
    private static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_MICA_EFFECT = 1029;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    // Current State
    public static ThemeMode CurrentTheme { get; private set; } = ThemeMode.Dark;
    public static OsStyle CurrentOsStyle { get; private set; } = OsStyle.Windows;
    public static bool? ForcedLightMode { get; private set; } = null;
    public static string? CustomAccentColorHex { get; private set; } = null;
    public static bool UseWindowsAccentColor { get; private set; } = false;
    public static bool ApplyAccentToBorder { get; private set; } = false;

    // Glass / Transparency State
    public static bool IsGlassEffectEnabled { get; private set; } = true;
    public static string? CustomGlassTintHex { get; private set; } = null;
    public static double GlassOpacity { get; private set; } = 0.85;
    public static bool GlassTintWithAccent { get; private set; } = false;

    // Events
    public static event Action<ThemeMode>? ThemeChanged;
    public static event Action<OsStyle>? OsStyleChanged;
    public static event Action<string?>? AccentColorChanged;
    public static event Action? AppearanceChanged;

    /// <summary>
    /// Indicates whether the active visual presentation is light mode.
    /// Supports dynamic toggle for any selected theme.
    /// </summary>
    public static bool IsLightMode
    {
        get
        {
            if (ForcedLightMode.HasValue)
            {
                return ForcedLightMode.Value;
            }
            if (CurrentTheme == ThemeMode.Light) return true;
            if (CurrentTheme == ThemeMode.System) return IsWindowsInLightMode();
            return false;
        }
    }

    /// <summary>
    /// Active accent color currently rendered across the app.
    /// </summary>
    public static string ActiveAccentHex
    {
        get
        {
            if (UseWindowsAccentColor)
            {
                return GetWindowsAccentColorHex();
            }
            if (!string.IsNullOrWhiteSpace(CustomAccentColorHex))
            {
                return CustomAccentColorHex;
            }
            return GetDefaultAccentHex(CurrentTheme);
        }
    }

    /// <summary>
    /// Active glass tint color.
    /// </summary>
    public static string ActiveGlassTintHex
    {
        get
        {
            if (IsLightMode)
            {
                return "#FFFFFF";
            }
            if (GlassTintWithAccent)
            {
                return ActiveAccentHex;
            }
            if (!string.IsNullOrWhiteSpace(CustomGlassTintHex))
            {
                return CustomGlassTintHex;
            }
            return GetDefaultGlassTintHex(CurrentTheme);
        }
    }

    /// <summary>
    /// Load all stored appearance settings and apply them simultaneously.
    /// </summary>
    public static void LoadConfiguration(
        ThemeMode theme,
        string? accentHex,
        bool useWindowsAccent,
        bool accentOnBorder,
        bool glassEnabled,
        string? glassTintHex,
        double glassOpacity,
        bool glassTintWithAccent,
        OsStyle osStyle = OsStyle.Windows,
        bool? forcedLightMode = null)
    {
        CurrentTheme = theme;
        CurrentOsStyle = osStyle;
        ForcedLightMode = forcedLightMode;
        CustomAccentColorHex = string.IsNullOrWhiteSpace(accentHex) ? null : NormalizeHex(accentHex);
        UseWindowsAccentColor = useWindowsAccent;
        ApplyAccentToBorder = accentOnBorder;
        IsGlassEffectEnabled = glassEnabled;
        CustomGlassTintHex = string.IsNullOrWhiteSpace(glassTintHex) ? null : NormalizeHex(glassTintHex);
        GlassOpacity = Math.Clamp(glassOpacity, 0.35, 0.98);
        GlassTintWithAccent = glassTintWithAccent;

        ApplyAppearance();
    }

    public static void ApplyTheme(ThemeMode theme)
    {
        CurrentTheme = theme;
        CustomAccentColorHex = null;
        CustomGlassTintHex = null;
        UseWindowsAccentColor = (theme == ThemeMode.System);
        ApplyAppearance();
    }

    public static void SetOsStyle(OsStyle os)
    {
        CurrentOsStyle = os;
        ApplyAppearance();
        OsStyleChanged?.Invoke(os);
    }

    public static void SetLightMode(bool? isLight)
    {
        ForcedLightMode = isLight;
        ApplyAppearance();
    }

    public static void ToggleLightDarkMode()
    {
        ForcedLightMode = !IsLightMode;
        ApplyAppearance();
    }

    public static bool ApplyCustomAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            CustomAccentColorHex = null;
            UseWindowsAccentColor = false;
            ApplyAppearance();
            return true;
        }

        try
        {
            var normalized = NormalizeHex(hex);
            ColorFromHex(normalized);
            CustomAccentColorHex = normalized;
            UseWindowsAccentColor = false;
            ApplyAppearance();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SetUseWindowsAccentColor(bool useWindows)
    {
        UseWindowsAccentColor = useWindows;
        ApplyAppearance();
    }

    public static void SetApplyAccentToBorder(bool applyToBorder)
    {
        ApplyAccentToBorder = applyToBorder;
        ApplyAppearance();
    }

    public static void SetGlassEffect(bool enabled)
    {
        IsGlassEffectEnabled = enabled;
        ApplyAppearance();
    }

    public static bool SetGlassTint(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            CustomGlassTintHex = null;
            ApplyAppearance();
            return true;
        }

        try
        {
            var normalized = NormalizeHex(hex);
            ColorFromHex(normalized);
            CustomGlassTintHex = normalized;
            ApplyAppearance();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SetGlassOpacity(double opacity)
    {
        GlassOpacity = Math.Clamp(opacity, 0.35, 0.98);
        ApplyAppearance();
    }

    public static void SetGlassTintWithAccent(bool tintWithAccent)
    {
        GlassTintWithAccent = tintWithAccent;
        ApplyAppearance();
    }

    public static void EnableWindowBlur(Window window)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) return;

            IntPtr hwnd = helper.Handle;

            // 1. Windows 11 (Build 22000+) Native Immersive Dark Mode for native dialogs/menus
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                int darkMode = IsLightMode ? 0 : 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }

            // NOTE: Do NOT call SetWindowCompositionAttribute (ACCENT_ENABLE_BLURBEHIND)
            // or DWMWA_SYSTEMBACKDROP_TYPE on WPF windows with AllowsTransparency="True".
            // In Windows 10 (1809+) and Windows 11, invoking DWM backdrop or SetWindowCompositionAttribute
            // on layered windows (WS_EX_LAYERED) causes DWM to paint the transparent margins,
            // drop shadows, and rounded corners with a solid, opaque black rectangular box!
            // WPF's native per-pixel alpha rendering pipeline handles transparency and rounded corners cleanly.
        }
        catch { }
    }

    /// <summary>
    /// Re-computes and updates all Application Resource dictionaries with active theme, glass tint and accent.
    /// </summary>
    public static void ApplyAppearance()
    {
        var appResources = Application.Current?.Resources;
        if (appResources == null) return;

        bool isLight = IsLightMode;

        // 1. Resolve Effective Base Theme
        ThemeMode effectiveBaseTheme;
        if (CurrentTheme == ThemeMode.System)
        {
            effectiveBaseTheme = isLight ? ThemeMode.Light : ThemeMode.Dark;
        }
        else if (CurrentTheme == ThemeMode.Glass)
        {
            effectiveBaseTheme = ThemeMode.Glass;
        }
        else
        {
            effectiveBaseTheme = CurrentTheme;
        }

        bool isGlass = CurrentTheme == ThemeMode.Glass || IsGlassEffectEnabled;

        // 2. Set OS Style Dimensions and Visibility Resources
        bool isLinux = CurrentOsStyle == OsStyle.Linux;
        appResources["WindowCornerRadius"] = isLinux ? new CornerRadius(20) : new CornerRadius(14);
        appResources["WindowCornerRadiusTop"] = isLinux ? new CornerRadius(20, 20, 0, 0) : new CornerRadius(14, 14, 0, 0);
        appResources["IsLinuxStyle"] = isLinux;
        appResources["IsWindowsStyle"] = !isLinux;
        appResources["LinuxVisibility"] = isLinux ? Visibility.Visible : Visibility.Collapsed;
        appResources["WindowsVisibility"] = !isLinux ? Visibility.Visible : Visibility.Collapsed;

        // 3. Apply Theme Base Palette (Typography, Card, Popup, Scrollbar)
        ApplyBaseThemePalette(appResources, effectiveBaseTheme, isLight);

        // 4. Resolve Effective Accent Color
        string accentHex = ActiveAccentHex;
        Color accentColor;
        try
        {
            accentColor = ColorFromHex(accentHex);
        }
        catch
        {
            accentHex = GetDefaultAccentHex(effectiveBaseTheme);
            accentColor = ColorFromHex(accentHex);
        }

        // 5. Apply Glass Surface & Specular Highlights
        if (isGlass)
        {
            ApplyGlassSurface(appResources, effectiveBaseTheme, isLight, accentColor);
        }
        else
        {
            if (ApplyAccentToBorder)
            {
                SetBrush(appResources, "WindowBorderBrush", accentColor);
            }
        }

        // 6. Apply Accent Color to all controls
        ApplyAccentBrushes(appResources, accentColor, isLight, isGlass);

        // 7. Notify subscribers
        ThemeChanged?.Invoke(CurrentTheme);
        AccentColorChanged?.Invoke(accentHex);
        AppearanceChanged?.Invoke();
    }

    static ThemeManager()
    {
        try
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (CurrentTheme == ThemeMode.System || UseWindowsAccentColor)
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        ApplyAppearance();
                    });
                }
            };
        }
        catch { }
    }

    private static void ApplyBaseThemePalette(ResourceDictionary appResources, ThemeMode theme, bool isLight)
    {
        if (isLight)
        {
            ApplyBaseThemeLightPalette(appResources, theme);
        }
        else
        {
            ApplyBaseThemeDarkPalette(appResources, theme);
        }
    }

    private static void ApplyBaseThemeDarkPalette(ResourceDictionary appResources, ThemeMode theme)
    {
        switch (theme)
        {
            case ThemeMode.Ubuntu:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#1E1322"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#3A2337"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#2C1832"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#4A2E46"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#F7F6F5"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#E6C29E"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#9E7A8E"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#E6C29E"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#E95420"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#E95420"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#351F3D"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#472551"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#E95420"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#25152A"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#4A2E46"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#25152A"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#4A2E46"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#3A2337"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#2E1934"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#4E2C4B"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#FB703C"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#38193D"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#612469"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#FB703C"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#241629"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#3A2337"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#633959"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#E95420"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#221626"));
                SetBrush(appResources, "KbdBgBrush", ColorFromHex("#2E1934"));
                SetBrush(appResources, "KbdBorderBrush", ColorFromHex("#4E2C4B"));
                SetBrush(appResources, "KbdForegroundBrush", ColorFromHex("#F7F6F5"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#100A12"));
                appResources["ShadowOpacity"] = 0.65;
                break;

            case ThemeMode.Arch:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#111827"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#1F2937"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#162033"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#28364D"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#F0F9FF"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#BAE6FD"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#64748B"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#7DD3FC"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#00AAD4"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#38BDF8"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#1E2C44"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#22395C"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#00AAD4"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#141D2E"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#28364D"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#141D2E"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#28364D"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#1F2937"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#17243B"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#283D61"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#38BDF8"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#152C4A"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#1B4578"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#38BDF8"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#141E30"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#21304A"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#2D4367"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#00AAD4"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#141C2B"));
                SetBrush(appResources, "KbdBgBrush", ColorFromHex("#17243B"));
                SetBrush(appResources, "KbdBorderBrush", ColorFromHex("#283D61"));
                SetBrush(appResources, "KbdForegroundBrush", ColorFromHex("#F0F9FF"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#070B12"));
                appResources["ShadowOpacity"] = 0.65;
                break;

            case ThemeMode.Aurora:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#0E122B"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#262252"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#161A3C"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#342D6D"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#F5F3FF"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#DDD6FE"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#7C739E"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#C4B5FD"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#8B5CF6"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#06B6D4"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#212052"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#2D2670"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#8B5CF6"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#121535"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#342D6D"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#121535"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#342D6D"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#262252"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#1F1C4A"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#3A3078"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#A78BFA"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#221A52"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#3F2D8F"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#A78BFA"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#131637"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#232356"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#3B3475"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#8B5CF6"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#131637"));
                SetBrush(appResources, "KbdBgBrush", ColorFromHex("#1F1C4A"));
                SetBrush(appResources, "KbdBorderBrush", ColorFromHex("#3A3078"));
                SetBrush(appResources, "KbdForegroundBrush", ColorFromHex("#F5F3FF"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#080A1A"));
                appResources["ShadowOpacity"] = 0.70;
                break;

            case ThemeMode.Cyberpunk:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#0D0B18"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#F43F5E"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#17132B"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#E11D48"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#E0E7FF"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#938BB8"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#FDA4AF"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#F43F5E"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#06B6D4"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#2B1D45"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#3D1645"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#F43F5E"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#130F29"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#F43F5E"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#130F29"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#F43F5E"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#2B1D45"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#241538"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#F43F5E"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#FDA4AF"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#3306B6D4"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#06B6D4"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#67E8F9"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#17132B"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#3D275A"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#E11D48"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#FB7185"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#23153A"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#F43F5E"));
                appResources["ShadowOpacity"] = 0.35;
                break;

            case ThemeMode.Nord:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#242933"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#434C5E"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#2E3440"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#4C566A"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#ECEFF4"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#D8DEE9"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#7B88A1"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#D8DEE9"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#88C0D0"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#81A1C1"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#2F3646"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#3B4252"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#88C0D0"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#242933"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#434C5E"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#242933"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#434C5E"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#3B4252"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#2E3440"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#4C566A"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#ECEFF4"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#2E3A4E"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#5E81AC"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#88C0D0"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#29303C"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#3E4758"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#4C566A"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#88C0D0"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#2E3440"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#1D212A"));
                appResources["ShadowOpacity"] = 0.65;
                break;

            case ThemeMode.Dracula:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#21222C"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#44475A"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#282A36"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#6272A4"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#F8F8F2"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#BD93F9"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#6272A4"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#BD93F9"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#BD93F9"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#FF79C6"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#343746"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#44475A"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#BD93F9"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#282A36"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#44475A"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#282A36"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#44475A"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#44475A"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#282A36"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#6272A4"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#50FA7B"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#3B2E58"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#BD93F9"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#FF79C6"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#282A36"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#3D4052"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#6272A4"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#BD93F9"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#282A36"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#15161E"));
                appResources["ShadowOpacity"] = 0.70;
                break;

            case ThemeMode.TokyoNight:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#1A1B26"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#2F3549"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#202336"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#3D4566"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#C0CAF5"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#9AA5CE"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#565F89"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#9AA5CE"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#7AA2F7"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#7DCFFF"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#24283B"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#2D334C"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#7AA2F7"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#1F2335"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#3D4566"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#1F2335"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#3D4566"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#2A2F45"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#24283B"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#3D4566"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#BB9AF7"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#222944"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#415385"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#7DCFFF"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#1F2335"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#292F47"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#414868"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#7AA2F7"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#1F2335"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#0F1017"));
                appResources["ShadowOpacity"] = 0.70;
                break;

            case ThemeMode.Emerald:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#0C1814"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#1B382B"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#13261F"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#264D3B"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#ECFDF5"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#A7F3D0"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#527E6A"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#A7F3D0"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#10B981"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#34D399"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#183329"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#1E4235"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#10B981"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#0F201A"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#264D3B"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#0F201A"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#264D3B"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#1B382B"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#142921"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#234937"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#6EE7B7"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#112E23"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#1B523E"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#34D399"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#11221B"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#1E3F31"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#2E624C"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#10B981"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#12241C"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#050B09"));
                appResources["ShadowOpacity"] = 0.75;
                break;

            case ThemeMode.Sunset:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#18111A"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#3A2337"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#231726"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#4A2E46"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#FFF1F2"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#FECDD3"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#8E6D87"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#FECDD3"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#FB923C"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#F43F5E"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#2C1B2E"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#3F223E"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#FB7185"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#201422"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#4A2E46"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#201422"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#4A2E46"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#3A2337"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#2A1A2B"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#4E2C4B"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#FDA4AF"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#381B28"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#612B43"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#FB923C"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#201524"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#352033"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#633959"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#FB923C"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#221626"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#0F0A11"));
                appResources["ShadowOpacity"] = 0.75;
                break;

            case ThemeMode.Linux:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#242424"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#414141"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#303030"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#4A4A4A"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#F6F5F4"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#D0CFCC"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#A3A29E"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#C0BFBC"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#8FF0A4"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#8FF0A4"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#383838"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#35483A"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#8FF0A4"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#2B2B2B"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#4A4A4A"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#2B2B2B"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#4A4A4A"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#414141"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#353535"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#505050"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#E6E5E2"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#35483A"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#55735C"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#A8F0B5"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#2B2B2B"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#414141"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#686868"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#8FF0A4"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#303030"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#111111"));
                appResources["ShadowOpacity"] = 0.55;
                break;

            case ThemeMode.Dark:
            default:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#0F121C"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#222838"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#161A28"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#2C344A"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#F8FAFC"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#CBD5E1"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#8492A6"));
                SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#94A3B8"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#3B82F6"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#60A5FA"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#22293C"));
                SetBrush(appResources, "ItemSelectedBgBrush", Color.FromArgb(45, 59, 130, 246));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#3B82F6"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#121622"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#2C344A"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#121622"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#2C344A"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#222838"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#181E30"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#283452"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#60A5FA"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#1A243D"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#2B4378"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#60A5FA"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#181C2B"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#252D42"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#384462"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#3B82F6"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#161A28"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#000000"));
                appResources["ShadowOpacity"] = 0.65;
                break;
        }

        // Dark universal defaults
        SetBrush(appResources, "KbdBgBrush", ColorFromHex("#262A34"));
        SetBrush(appResources, "KbdBorderBrush", ColorFromHex("#383D4C"));
        SetBrush(appResources, "KbdForegroundBrush", ColorFromHex("#E2E8F0"));
        SetBrush(appResources, "EmptyStateBrush", ColorFromHex("#64748B"));
        SetBrush(appResources, "IconBrush", ColorFromHex("#FFFFFF"));
        SetBrush(appResources, "GlassRimBrush", Color.FromArgb(36, 255, 255, 255));
    }

    private static void ApplyBaseThemeLightPalette(ResourceDictionary appResources, ThemeMode theme)
    {
        string accent = GetDefaultAccentHex(theme);
        Color accentColor;
        try { accentColor = ColorFromHex(accent); }
        catch { accentColor = ColorFromHex("#3B82F6"); }

        // Light background base (Clean Frosted Liquid Glass / Milk Glass)
        Color windowBg = ColorFromHex("#FFFFFF");
        Color windowBorder = ColorFromHex("#CBD5E1");
        Color searchBg = ColorFromHex("#FFFFFF");
        Color searchBorder = ColorFromHex("#CBD5E1");

        // Specific light tint adjustments for artistic themes (Exact match with Web Studio)
        switch (theme)
        {
            case ThemeMode.Ubuntu:
                accentColor = ColorFromHex("#E95420");
                break;
            case ThemeMode.Arch:
                accentColor = ColorFromHex("#00AAD4");
                break;
            case ThemeMode.Aurora:
                accentColor = ColorFromHex("#8B5CF6");
                break;
            case ThemeMode.Cyberpunk:
                accentColor = ColorFromHex("#F43F5E");
                break;
            case ThemeMode.Nord:
                accentColor = ColorFromHex("#88C0D0");
                break;
            case ThemeMode.Dracula:
                accentColor = ColorFromHex("#BD93F9");
                break;
            case ThemeMode.Emerald:
                accentColor = ColorFromHex("#10B981");
                break;
            case ThemeMode.Sunset:
                accentColor = ColorFromHex("#FB923C");
                break;
            case ThemeMode.Linux:
                accentColor = ColorFromHex("#16A34A");
                break;
            default:
                accentColor = ColorFromHex("#3B82F6");
                break;
        }

        SetBrush(appResources, "WindowBgBrush", windowBg);
        SetBrush(appResources, "WindowBorderBrush", windowBorder);
        SetBrush(appResources, "SearchBarBgBrush", searchBg);
        SetBrush(appResources, "SearchBarBorderBrush", searchBorder);
        SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#0F172A"));
        SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#475569"));
        SetBrush(appResources, "TextMutedBrush", ColorFromHex("#64748B"));
        SetBrush(appResources, "IconBrush", ColorFromHex("#1E293B"));
        SetBrush(appResources, "IconMutedBrush", ColorFromHex("#64748B"));
        SetBrush(appResources, "AccentBrush", accentColor);
        SetBrush(appResources, "LogoBrush", accentColor);
        SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#F1F5F9"));
        SetBrush(appResources, "ItemSelectedBgBrush", Color.FromArgb(28, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "ItemSelectedBorderBrush", Color.FromArgb(160, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#FFFFFF"));
        SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#E2E8F0"));
        SetBrush(appResources, "PopupBgBrush", ColorFromHex("#FFFFFF"));
        SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#E2E8F0"));
        SetBrush(appResources, "SeparatorBrush", ColorFromHex("#E2E8F0"));
        SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#F1F5F9"));
        SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#E2E8F0"));
        SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#475569"));
        SetBrush(appResources, "DateBadgeBgBrush", Color.FromArgb(24, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "DateBadgeBorderBrush", Color.FromArgb(120, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "DateBadgeForegroundBrush", accentColor);
        SetBrush(appResources, "CardBgBrush", ColorFromHex("#F8FAFC"));
        SetBrush(appResources, "CardBorderBrush", ColorFromHex("#E2E8F0"));
        SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#CBD5E1"));
        SetBrush(appResources, "ScrollBarThumbHoverBrush", accentColor);
        SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#F1F5F9"));
        SetBrush(appResources, "KbdBgBrush", ColorFromHex("#FFFFFF"));
        SetBrush(appResources, "KbdBorderBrush", ColorFromHex("#CBD5E1"));
        SetBrush(appResources, "KbdForegroundBrush", ColorFromHex("#1E293B"));
        SetBrush(appResources, "EmptyStateBrush", ColorFromHex("#64748B"));
        SetBrush(appResources, "SelectionIndicatorBrush", accentColor);
        SetBrush(appResources, "GlassRimBrush", Color.FromArgb(220, 255, 255, 255));
        SetColor(appResources, "ShadowColor", ColorFromHex("#0F172A"));
        appResources["ShadowOpacity"] = 0.14;
    }

    private static void ApplyGlassSurface(ResourceDictionary appResources, ThemeMode theme, bool isLight, Color accentColor)
    {
        // In Light Mode, ALWAYS use pure frosted milk-glass / liquid crystal white!
        // Never allow a dark custom tint (e.g. from Dark Mode) to contaminate Light Mode.
        Color tintColor;
        if (isLight)
        {
            tintColor = ColorFromHex("#FFFFFF");
        }
        else if (GlassTintWithAccent)
        {
            Color foundation = ColorFromHex("#090B11");
            tintColor = BlendColors(foundation, accentColor, 0.28);
        }
        else if (!string.IsNullOrWhiteSpace(CustomGlassTintHex))
        {
            tintColor = ColorFromHex(CustomGlassTintHex);
        }
        else
        {
            tintColor = ColorFromHex(GetDefaultGlassTintHex(theme));
        }

        byte alpha = isLight 
            ? (byte)Math.Clamp((int)(GlassOpacity * 255), 215, 248) 
            : (byte)Math.Clamp((int)(GlassOpacity * 255), 70, 248);

        // Window Background Brush (Glass Translucent)
        SetBrush(appResources, "WindowBgBrush", isLight 
            ? Color.FromArgb(alpha, 255, 255, 255) 
            : Color.FromArgb(alpha, tintColor.R, tintColor.G, tintColor.B));

        // Search Bar (Translucent Glass Inset)
        if (isLight)
        {
            SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#FFFFFF"));
            SetBrush(appResources, "SearchBarBorderBrush", Color.FromArgb(180, 203, 213, 225));
        }
        else
        {
            byte barAlpha = (byte)Math.Min(245, alpha + 28);
            byte barR = (byte)Math.Min(255, tintColor.R + 16);
            byte barG = (byte)Math.Min(255, tintColor.G + 16);
            byte barB = (byte)Math.Min(255, tintColor.B + 22);
            SetBrush(appResources, "SearchBarBgBrush", Color.FromArgb(barAlpha, barR, barG, barB));
            SetBrush(appResources, "SearchBarBorderBrush", Color.FromArgb(100, (byte)Math.Min(255, barR + 60), (byte)Math.Min(255, barG + 60), (byte)Math.Min(255, barB + 80)));
        }

        // Window Border
        if (ApplyAccentToBorder)
        {
            SetBrush(appResources, "WindowBorderBrush", Color.FromArgb(170, accentColor.R, accentColor.G, accentColor.B));
        }
        else
        {
            if (isLight)
            {
                SetBrush(appResources, "WindowBorderBrush", Color.FromArgb(200, 203, 213, 225));
            }
            else
            {
                byte borderR = (byte)Math.Min(255, tintColor.R + 65);
                byte borderG = (byte)Math.Min(255, tintColor.G + 65);
                byte borderB = (byte)Math.Min(255, tintColor.B + 85);
                SetBrush(appResources, "WindowBorderBrush", Color.FromArgb(110, borderR, borderG, borderB));
            }
        }

        // Card & Popup (Frosted panels)
        if (isLight)
        {
            SetBrush(appResources, "CardBgBrush", Color.FromArgb(220, 248, 250, 252));
            SetBrush(appResources, "PopupBgBrush", Color.FromArgb(250, 255, 255, 255));
            SetBrush(appResources, "ContextMenuBgBrush", Color.FromArgb(250, 255, 255, 255));
            SetBrush(appResources, "KbdBgBrush", ColorFromHex("#FFFFFF"));
            SetBrush(appResources, "KbdBorderBrush", ColorFromHex("#CBD5E1"));
            SetBrush(appResources, "KbdForegroundBrush", ColorFromHex("#1E293B"));
            SetBrush(appResources, "SeparatorBrush", ColorFromHex("#E2E8F0"));
            SetBrush(appResources, "EmptyStateBrush", ColorFromHex("#64748B"));
            SetBrush(appResources, "SelectionIndicatorBrush", accentColor);
            SetBrush(appResources, "GlassRimBrush", Color.FromArgb(230, 255, 255, 255));
        }
        else
        {
            byte cardAlpha = (byte)Math.Clamp(alpha - 30, 45, 220);
            SetBrush(appResources, "CardBgBrush", Color.FromArgb(cardAlpha, tintColor.R, tintColor.G, tintColor.B));
            SetBrush(appResources, "PopupBgBrush", Color.FromArgb(240, tintColor.R, tintColor.G, tintColor.B));
            SetBrush(appResources, "ContextMenuBgBrush", Color.FromArgb(240, tintColor.R, tintColor.G, tintColor.B));
            SetBrush(appResources, "KbdBgBrush", Color.FromArgb(140, tintColor.R, tintColor.G, tintColor.B));
            SetBrush(appResources, "KbdBorderBrush", Color.FromArgb(100, (byte)Math.Min(255, tintColor.R + 60), (byte)Math.Min(255, tintColor.G + 60), (byte)Math.Min(255, tintColor.B + 80)));
            SetBrush(appResources, "KbdForegroundBrush", ColorFromHex("#F8FAFC"));
            SetBrush(appResources, "EmptyStateBrush", ColorFromHex("#94A3B8"));
            SetBrush(appResources, "SelectionIndicatorBrush", accentColor);
            SetBrush(appResources, "GlassRimBrush", Color.FromArgb(40, 255, 255, 255));
        }

        // Specular gradient brush (Crisp white rim highlight)
        var specular = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        specular.GradientStops.Add(new GradientStop(isLight ? Color.FromArgb(230, 255, 255, 255) : Color.FromArgb(45, 255, 255, 255), 0.0));
        specular.GradientStops.Add(new GradientStop(isLight ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(8, 255, 255, 255), 0.35));
        specular.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
        specular.Freeze();
        appResources["GlassSpecularBrush"] = specular;

        // Shadows
        if (ApplyAccentToBorder)
        {
            SetColor(appResources, "ShadowColor", accentColor);
            appResources["ShadowOpacity"] = isLight ? 0.25 : 0.50;
        }
        else
        {
            SetColor(appResources, "ShadowColor", ColorFromHex("#0F172A"));
            appResources["ShadowOpacity"] = isLight ? 0.14 : 0.65;
        }
    }

    private static void ApplyAccentBrushes(ResourceDictionary appResources, Color accentColor, bool isLight, bool isGlass)
    {
        SetBrush(appResources, "AccentBrush", accentColor);
        appResources["AccentColor"] = accentColor;
        if (appResources["LogoBrush"] is SolidColorBrush lb)
        {
            appResources["LogoColor"] = lb.Color;
        }
        else
        {
            appResources["LogoColor"] = accentColor;
        }

        SetBrush(appResources, "SelectionIndicatorBrush", accentColor);
        SetBrush(appResources, "ItemSelectedBorderBrush", accentColor);
        SetBrush(appResources, "ScrollBarThumbHoverBrush", accentColor);

        // Highlight selection background with subtle accent glow
        byte selectAlpha = isLight ? (byte)32 : (byte)48;
        SetBrush(appResources, "ItemSelectedBgBrush", Color.FromArgb(selectAlpha, accentColor.R, accentColor.G, accentColor.B));

        // Date Badge Brushes
        byte badgeBgAlpha = isLight ? (byte)28 : (byte)40;
        SetBrush(appResources, "DateBadgeBgBrush", Color.FromArgb(badgeBgAlpha, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "DateBadgeBorderBrush", Color.FromArgb(100, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "DateBadgeForegroundBrush", accentColor);

        // If Solid mode and user wants accent on border
        if (!isGlass && ApplyAccentToBorder)
        {
            SetBrush(appResources, "WindowBorderBrush", accentColor);
        }
    }

    public static string GetDefaultGlassTintHex(ThemeMode theme)
    {
        return theme switch
        {
            ThemeMode.Ubuntu => "#1E1322",
            ThemeMode.Arch => "#111827",
            ThemeMode.Aurora => "#0E122B",
            ThemeMode.Cyberpunk => "#0D0B18",
            ThemeMode.Nord => "#242933",
            ThemeMode.Dracula => "#21222C",
            ThemeMode.Emerald => "#0C1814",
            ThemeMode.Sunset => "#18111A",
            ThemeMode.Linux => "#242424",
            _ => "#0B132B" // Dark, Light, System, Glass (Windows Fluent Blue)
        };
    }

    public static string GetDefaultAccentHex(ThemeMode theme)
    {
        return theme switch
        {
            ThemeMode.Light => "#3B82F6",
            ThemeMode.Glass => "#3B82F6",
            ThemeMode.Cyberpunk => "#F43F5E",
            ThemeMode.Nord => "#88C0D0",
            ThemeMode.Dracula => "#BD93F9",
            ThemeMode.TokyoNight => "#7AA2F7",
            ThemeMode.Emerald => "#10B981",
            ThemeMode.Linux => "#8FF0A4",
            ThemeMode.Sunset => "#FB923C",
            ThemeMode.Ubuntu => "#E95420",
            ThemeMode.Arch => "#00AAD4",
            ThemeMode.Aurora => "#8B5CF6",
            _ => "#3B82F6" // Dark & System (Windows Fluent Blue)
        };
    }

    /// <summary>
    /// Gets the Windows System Accent Color using DwmGetColorizationColor or registry fallback.
    /// </summary>
    public static string GetWindowsAccentColorHex()
    {
        try
        {
            if (DwmGetColorizationColor(out uint pcrColorization, out _) == 0)
            {
                byte r = (byte)((pcrColorization >> 16) & 0xFF);
                byte g = (byte)((pcrColorization >> 8) & 0xFF);
                byte b = (byte)(pcrColorization & 0xFF);
                return $"#{r:X2}{g:X2}{b:X2}";
            }
        }
        catch { }

        try
        {
            var dwmVal = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "ColorizationColor", null);
            if (dwmVal is int intVal)
            {
                uint u = (uint)intVal;
                byte r = (byte)((u >> 16) & 0xFF);
                byte g = (byte)((u >> 8) & 0xFF);
                byte b = (byte)(u & 0xFF);
                return $"#{r:X2}{g:X2}{b:X2}";
            }
        }
        catch { }

        return "#0078D7"; // Windows 11 default electric blue
    }

    /// <summary>
    /// Checks if Windows is currently set to Apps Light Theme.
    /// </summary>
    public static bool IsWindowsInLightMode()
    {
        try
        {
            var val = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                0);
            if (val is int intVal && intVal == 1)
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    private static Color BlendColors(Color baseColor, Color overlayColor, double overlayRatio)
    {
        overlayRatio = Math.Clamp(overlayRatio, 0.0, 1.0);
        byte r = (byte)(baseColor.R * (1 - overlayRatio) + overlayColor.R * overlayRatio);
        byte g = (byte)(baseColor.G * (1 - overlayRatio) + overlayColor.G * overlayRatio);
        byte b = (byte)(baseColor.B * (1 - overlayRatio) + overlayColor.B * overlayRatio);
        return Color.FromRgb(r, g, b);
    }

    private static void SetBrush(ResourceDictionary res, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        res[key] = brush;
    }

    private static void SetColor(ResourceDictionary res, string key, Color color)
    {
        res[key] = color;
    }

    public static Color ColorFromHex(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(NormalizeHex(hex));
    }

    private static string NormalizeHex(string hex)
    {
        var clean = hex.Trim();
        if (!clean.StartsWith('#'))
        {
            clean = "#" + clean;
        }
        return clean;
    }
}
