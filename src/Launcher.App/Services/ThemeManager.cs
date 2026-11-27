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
    Aurora,
    Windows11,
    Windows10,
    WindowsVista
}

public enum OsStyle
{
    Windows,
    Linux
}

public enum UiStyle
{
    ModernGlass,
    CompactCommand,
    SharpCyber,
    PillFloating,
    ClassicCard
}

public static class ThemeManager
{
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetColorizationColor")]
    private static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly record struct LightPaletteSpec(
        string WindowTintHex,
        string SidebarTintHex,
        string CardTintHex,
        string AccentHex,
        string IconInkHex,
        string PrimaryInkHex,
        string? VistaGradTop = null,
        string? VistaGradMid = null,
        string? VistaGradBot = null);

    private static LightPaletteSpec GetLightPaletteSpec(ThemeMode theme) => theme switch
    {
        ThemeMode.Windows11 => new("#DDEAF9", "#CFE1F6", "#F2F7FE", "#0067C0", "#0B5CAD", "#132238"),
        ThemeMode.Windows10 => new("#D8E7F6", "#C9DDF2", "#EEF5FC", "#0078D7", "#005EA8", "#102033"),
        ThemeMode.WindowsVista => new("#C9ECFB", "#B4E3F8", "#ECF9FF", "#0284C7", "#0369A1", "#0C2A42", "#BFEAFB", "#DFF6FF", "#C6ECFC"),
        ThemeMode.Cyberpunk => new("#FFE0EC", "#FCD0E2", "#FFF3F8", "#E11D48", "#BE123C", "#380E1E"),
        ThemeMode.Emerald => new("#D5F3E5", "#C2ECD8", "#EEFCF5", "#059669", "#047857", "#0B3325"),
        ThemeMode.Nord => new("#DCE6F2", "#CCD9EA", "#F0F5FA", "#4C759E", "#36597D", "#1E2C3E"),
        ThemeMode.Dracula => new("#E8DFFC", "#DACDF9", "#F6F2FF", "#7C3AED", "#6D28D9", "#24173B"),
        ThemeMode.Sunset => new("#FFE5D4", "#FCD6BE", "#FFF5EE", "#EA580C", "#C2410C", "#3B1A0B"),
        ThemeMode.Ubuntu => new("#F5E1DA", "#EBD0C6", "#FCF3F0", "#E95420", "#C43E10", "#33171D"),
        ThemeMode.Arch => new("#D4F0FA", "#C0E7F6", "#EEF9FE", "#0284C7", "#0369A1", "#0D2B3E"),
        ThemeMode.Aurora => new("#E3DDFC", "#D5CCFA", "#F4F1FF", "#7C3AED", "#6D28D9", "#1D1742"),
        ThemeMode.TokyoNight => new("#DEE5F9", "#CED9F5", "#F2F5FD", "#3D59A1", "#2E4687", "#1A223B"),
        ThemeMode.Linux => new("#DEF2E4", "#CBEAD5", "#F1FBF4", "#16A34A", "#15803D", "#132E1E"),
        _ => new("#E1ECFA", "#D4E4F7", "#F4F8FE", "#2563EB", "#1D4ED8", "#152338")
    };

    // Current State
    public static ThemeMode CurrentTheme { get; private set; } = ThemeMode.Dark;
    public static OsStyle CurrentOsStyle { get; private set; } = OsStyle.Windows;
    public static UiStyle CurrentUiStyle { get; private set; } = UiStyle.ModernGlass;
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
    public static event Action<UiStyle>? UiStyleChanged;
    public static event Action<string?>? AccentColorChanged;
    public static event Action? AppearanceChanged;

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

    public static string ActiveGlassTintHex
    {
        get
        {
            if (GlassTintWithAccent)
            {
                return ActiveAccentHex;
            }
            if (!string.IsNullOrWhiteSpace(CustomGlassTintHex))
            {
                return CustomGlassTintHex;
            }
            if (IsLightMode)
            {
                return GetLightPaletteSpec(CurrentTheme).WindowTintHex;
            }
            return GetDefaultGlassTintHex(CurrentTheme);
        }
    }

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
        bool? forcedLightMode = null,
        UiStyle uiStyle = UiStyle.ModernGlass)
    {
        CurrentTheme = theme;
        CurrentOsStyle = osStyle;
        CurrentUiStyle = uiStyle;
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
        if (theme == ThemeMode.Light)
        {
            ForcedLightMode = true;
        }
        else if (theme == ThemeMode.Dark)
        {
            ForcedLightMode = false;
        }
        CustomAccentColorHex = null;
        CustomGlassTintHex = null;
        UseWindowsAccentColor = (theme == ThemeMode.System);
        ApplyAppearance();
    }

    /// <summary>
    /// Applies a color/theme preset while preserving the user's current Light/Dark choice
    /// (except when switching to <see cref="ThemeMode.System"/>, which syncs with Windows).
    /// </summary>
    public static void ApplyThemePreset(ThemeMode theme)
    {
        CurrentTheme = theme;
        if (theme == ThemeMode.System)
        {
            ForcedLightMode = null;
        }
        else if (theme == ThemeMode.Light)
        {
            ForcedLightMode = true;
        }
        CustomAccentColorHex = null;
        CustomGlassTintHex = null;
        UseWindowsAccentColor = (theme == ThemeMode.System);
        ApplyAppearance();
    }

    public static void SetOsStyle(OsStyle os)
    {
        CurrentOsStyle = os;
        if (os == OsStyle.Linux && (CurrentTheme == ThemeMode.Dark || CurrentTheme == ThemeMode.System))
        {
            CurrentTheme = ThemeMode.Ubuntu;
        }
        else if (os == OsStyle.Windows && (CurrentTheme == ThemeMode.Ubuntu || CurrentTheme == ThemeMode.Arch || CurrentTheme == ThemeMode.Linux))
        {
            CurrentTheme = ThemeMode.Windows11;
        }

        ApplyAppearance();
        OsStyleChanged?.Invoke(CurrentOsStyle);
    }

    public static void SetUiStyle(UiStyle uiStyle)
    {
        CurrentUiStyle = uiStyle;
        ApplyAppearance();
        UiStyleChanged?.Invoke(CurrentUiStyle);
    }

    public static UiStyle CycleUiStyle()
    {
        var values = Enum.GetValues<UiStyle>();
        int next = (Array.IndexOf(values, CurrentUiStyle) + 1) % values.Length;
        SetUiStyle(values[next]);
        return CurrentUiStyle;
    }

    public static string GetUiStyleDisplayName(UiStyle style) => style switch
    {
        UiStyle.ModernGlass => "Modern Glass (Mặc định)",
        UiStyle.CompactCommand => "Gọn nhẹ (Compact)",
        UiStyle.SharpCyber => "Vuông vắn (Cyber)",
        UiStyle.PillFloating => "Viên thuốc (Pill)",
        UiStyle.ClassicCard => "Thẻ cổ điển (Classic)",
        _ => "Modern Glass"
    };

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

            if (Environment.OSVersion.Version.Build >= 22000)
            {
                int darkMode = IsLightMode ? 0 : 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
        }
        catch { }
    }

    public static void ApplyAppearance()
    {
        var appResources = Application.Current?.Resources;
        if (appResources == null) return;

        bool isLight = IsLightMode;

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

        bool isLinux = CurrentOsStyle == OsStyle.Linux;
        appResources["IsLinuxStyle"] = isLinux;
        appResources["IsWindowsStyle"] = !isLinux;
        appResources["LinuxVisibility"] = isLinux ? Visibility.Visible : Visibility.Collapsed;
        appResources["WindowsVisibility"] = !isLinux ? Visibility.Visible : Visibility.Collapsed;

        ApplyBaseThemePalette(appResources, effectiveBaseTheme, isLight);
        ApplyUiStyleResources(appResources, effectiveBaseTheme, isLinux);

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

        ApplyAccentBrushes(appResources, accentColor, isLight, isGlass);

        ThemeChanged?.Invoke(CurrentTheme);
        AccentColorChanged?.Invoke(accentHex);
        AppearanceChanged?.Invoke();
    }

    private static void ApplyUiStyleResources(ResourceDictionary appResources, ThemeMode effectiveTheme, bool isLinux)
    {
        switch (CurrentUiStyle)
        {
            case UiStyle.CompactCommand:
                appResources["WindowCornerRadius"] = new CornerRadius(12);
                appResources["WindowCornerRadiusTop"] = new CornerRadius(12, 12, 0, 0);
                appResources["WindowBorderThickness"] = new Thickness(1);
                appResources["SearchCornerRadius"] = new CornerRadius(9);
                appResources["SearchBorderThickness"] = new Thickness(1);
                appResources["SearchPadding"] = new Thickness(12, 6, 12, 6);
                appResources["ItemCornerRadius"] = new CornerRadius(7);
                appResources["ItemPadding"] = new Thickness(8, 4, 8, 4);
                appResources["ItemMargin"] = new Thickness(0, 0.5, 0, 0.5);
                appResources["ItemBorderThickness"] = new Thickness(1);
                appResources["ItemDefaultBorderBrush"] = Brushes.Transparent;
                appResources["IndicatorCornerRadius"] = new CornerRadius(1.5);
                appResources["IconCornerRadius"] = new CornerRadius(7);
                appResources["IconBoxSize"] = 28.0;
                appResources["PillCornerRadius"] = new CornerRadius(6);
                appResources["QuickStripVisibility"] = Visibility.Collapsed;
                appResources["FooterVisibility"] = Visibility.Visible;
                break;

            case UiStyle.SharpCyber:
                appResources["WindowCornerRadius"] = new CornerRadius(4);
                appResources["WindowCornerRadiusTop"] = new CornerRadius(4, 4, 0, 0);
                appResources["WindowBorderThickness"] = new Thickness(2);
                appResources["SearchCornerRadius"] = new CornerRadius(3);
                appResources["SearchBorderThickness"] = new Thickness(2);
                appResources["SearchPadding"] = new Thickness(12, 8, 12, 8);
                appResources["ItemCornerRadius"] = new CornerRadius(3);
                appResources["ItemPadding"] = new Thickness(10, 6, 10, 6);
                appResources["ItemMargin"] = new Thickness(0, 1.5, 0, 1.5);
                appResources["ItemBorderThickness"] = new Thickness(1);
                appResources["ItemDefaultBorderBrush"] = Brushes.Transparent;
                appResources["IndicatorCornerRadius"] = new CornerRadius(0);
                appResources["IconCornerRadius"] = new CornerRadius(3);
                appResources["IconBoxSize"] = 32.0;
                appResources["PillCornerRadius"] = new CornerRadius(3);
                appResources["QuickStripVisibility"] = Visibility.Collapsed;
                appResources["FooterVisibility"] = Visibility.Visible;
                break;

            case UiStyle.PillFloating:
                appResources["WindowCornerRadius"] = new CornerRadius(24);
                appResources["WindowCornerRadiusTop"] = new CornerRadius(24, 24, 0, 0);
                appResources["WindowBorderThickness"] = new Thickness(1.5);
                appResources["SearchCornerRadius"] = new CornerRadius(22);
                appResources["SearchBorderThickness"] = new Thickness(1.5);
                appResources["SearchPadding"] = new Thickness(14, 9, 14, 9);
                appResources["ItemCornerRadius"] = new CornerRadius(16);
                appResources["ItemPadding"] = new Thickness(10, 7, 10, 7);
                appResources["ItemMargin"] = new Thickness(0, 2, 0, 2);
                appResources["ItemBorderThickness"] = new Thickness(1);
                appResources["ItemDefaultBorderBrush"] = Brushes.Transparent;
                appResources["IndicatorCornerRadius"] = new CornerRadius(3);
                appResources["IconCornerRadius"] = new CornerRadius(17);
                appResources["IconBoxSize"] = 34.0;
                appResources["PillCornerRadius"] = new CornerRadius(16);
                appResources["QuickStripVisibility"] = Visibility.Collapsed;
                appResources["FooterVisibility"] = Visibility.Visible;
                break;

            case UiStyle.ClassicCard:
                appResources["WindowCornerRadius"] = new CornerRadius(10);
                appResources["WindowCornerRadiusTop"] = new CornerRadius(10, 10, 0, 0);
                appResources["WindowBorderThickness"] = new Thickness(1.5);
                appResources["SearchCornerRadius"] = new CornerRadius(8);
                appResources["SearchBorderThickness"] = new Thickness(1.5);
                appResources["SearchPadding"] = new Thickness(12, 8, 12, 8);
                appResources["ItemCornerRadius"] = new CornerRadius(8);
                appResources["ItemPadding"] = new Thickness(10, 6, 10, 6);
                appResources["ItemMargin"] = new Thickness(0, 2, 0, 2);
                appResources["ItemBorderThickness"] = new Thickness(1);
                appResources["ItemDefaultBorderBrush"] = Brushes.Transparent;
                appResources["IndicatorCornerRadius"] = new CornerRadius(2);
                appResources["IconCornerRadius"] = new CornerRadius(8);
                appResources["IconBoxSize"] = 32.0;
                appResources["PillCornerRadius"] = new CornerRadius(6);
                appResources["QuickStripVisibility"] = Visibility.Collapsed;
                appResources["FooterVisibility"] = Visibility.Visible;
                break;

            case UiStyle.ModernGlass:
            default:
                if (effectiveTheme == ThemeMode.Windows10 && !isLinux)
                {
                    appResources["WindowCornerRadius"] = new CornerRadius(6);
                    appResources["WindowCornerRadiusTop"] = new CornerRadius(6, 6, 0, 0);
                    appResources["WindowBorderThickness"] = new Thickness(1);
                    appResources["SearchCornerRadius"] = new CornerRadius(4);
                    appResources["SearchBorderThickness"] = new Thickness(1.5);
                    appResources["SearchPadding"] = new Thickness(12, 8, 12, 8);
                    appResources["ItemCornerRadius"] = new CornerRadius(4);
                    appResources["ItemPadding"] = new Thickness(10, 6, 10, 6);
                    appResources["ItemMargin"] = new Thickness(0, 1, 0, 1);
                    appResources["ItemBorderThickness"] = new Thickness(1);
                    appResources["ItemDefaultBorderBrush"] = Brushes.Transparent;
                    appResources["IndicatorCornerRadius"] = new CornerRadius(1);
                    appResources["IconCornerRadius"] = new CornerRadius(6);
                    appResources["IconBoxSize"] = 32.0;
                    appResources["PillCornerRadius"] = new CornerRadius(4);
                    appResources["QuickStripVisibility"] = Visibility.Collapsed;
                    appResources["FooterVisibility"] = Visibility.Visible;
                }
                else
                {
                    double winRad = isLinux ? 20 : 14;
                    appResources["WindowCornerRadius"] = new CornerRadius(winRad);
                    appResources["WindowCornerRadiusTop"] = new CornerRadius(winRad, winRad, 0, 0);
                    appResources["WindowBorderThickness"] = new Thickness(1);
                    appResources["SearchCornerRadius"] = new CornerRadius(10);
                    appResources["SearchBorderThickness"] = new Thickness(1.5);
                    appResources["SearchPadding"] = new Thickness(12, 8, 12, 8);
                    appResources["ItemCornerRadius"] = new CornerRadius(8);
                    appResources["ItemPadding"] = new Thickness(10, 6, 10, 6);
                    appResources["ItemMargin"] = new Thickness(0, 1, 0, 1);
                    appResources["ItemBorderThickness"] = new Thickness(1);
                    appResources["ItemDefaultBorderBrush"] = Brushes.Transparent;
                    appResources["IndicatorCornerRadius"] = new CornerRadius(2);
                    appResources["IconCornerRadius"] = new CornerRadius(8);
                    appResources["IconBoxSize"] = 32.0;
                    appResources["PillCornerRadius"] = new CornerRadius(8);
                    appResources["QuickStripVisibility"] = Visibility.Collapsed;
                    appResources["FooterVisibility"] = Visibility.Visible;
                }
                break;
        }
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
            case ThemeMode.Windows11:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#1C212B"));
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#181C25"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#252B37"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#333D52"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#252B37"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#39445B"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E2E8F0"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#B5C2D6"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#8896AE"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#A5B6CE"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#0078D4"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#60CDFF"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#2B3342"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#253954"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#0078D4"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#202632"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#39445B"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#202632"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#39445B"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#2D3546"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#232A38"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#38435C"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#60CDFF"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#1D3352"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#2A4C7D"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#60CDFF"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#242A36"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#323A4C"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#46526B"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#0078D4"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#1D222D"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#090C12"));
                appResources["ShadowOpacity"] = 0.62;
                break;

            case ThemeMode.Windows10:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#141923"));
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#10141D"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#1D2432"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#0078D7"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#1B212E"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#313B4F"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E2E8F0"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#B6C2D4"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#8290A6"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#9CB0CC"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#0078D7"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#38BDF8"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#232C3D"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#1A3554"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#0078D7"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#171C27"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#334155"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#171C27"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#334155"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#252E3F"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#1C2331"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#2F3B52"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#60A5FA"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#162B47"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#254878"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#60A5FA"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#1B2230"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#2B3548"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#3B4861"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#0078D7"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#151A24"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#06080D"));
                appResources["ShadowOpacity"] = 0.65;
                break;

            case ThemeMode.WindowsVista:
                appResources["WindowBgBrush"] = CreateAeroGradientBrush(
                    ColorFromHex("#102B42"),
                    ColorFromHex("#0B1B2E"),
                    ColorFromHex("#123650"));
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#0B2034"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#15354F"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#38BDF8"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#102A40"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#255A80"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#DFECF7"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#B5D8EE"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#7BA3BF"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#7DD3FC"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#1BA1E2"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#38BDF8"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#173C5B"));
                SetBrush(appResources, "ItemSelectedBgBrush", ColorFromHex("#1B4D75"));
                SetBrush(appResources, "ItemSelectedBorderBrush", ColorFromHex("#38BDF8"));
                SetBrush(appResources, "ContextMenuBgBrush", ColorFromHex("#0E253A"));
                SetBrush(appResources, "ContextMenuBorderBrush", ColorFromHex("#2D6B99"));
                SetBrush(appResources, "PopupBgBrush", ColorFromHex("#0E253A"));
                SetBrush(appResources, "PopupBorderBrush", ColorFromHex("#2D6B99"));
                SetBrush(appResources, "SeparatorBrush", ColorFromHex("#1E4668"));
                SetBrush(appResources, "BadgeBgBrush", ColorFromHex("#12314B"));
                SetBrush(appResources, "BadgeBorderBrush", ColorFromHex("#27628F"));
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#7DD3FC"));
                SetBrush(appResources, "DateBadgeBgBrush", ColorFromHex("#143B5C"));
                SetBrush(appResources, "DateBadgeBorderBrush", ColorFromHex("#2B74A8"));
                SetBrush(appResources, "DateBadgeForegroundBrush", ColorFromHex("#7DD3FC"));
                SetBrush(appResources, "CardBgBrush", ColorFromHex("#132F48"));
                SetBrush(appResources, "CardBorderBrush", ColorFromHex("#25547B"));
                SetBrush(appResources, "ScrollBarThumbBrush", ColorFromHex("#2B6A99"));
                SetBrush(appResources, "ScrollBarThumbHoverBrush", ColorFromHex("#38BDF8"));
                SetBrush(appResources, "ScrollBarTrackBrush", ColorFromHex("#0D2134"));
                SetColor(appResources, "ShadowColor", ColorFromHex("#030D17"));
                appResources["ShadowOpacity"] = 0.72;
                break;

            case ThemeMode.Ubuntu:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#1E1322"));
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#19101C"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#2D1C33"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#3A2337"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#2C1832"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#4A2E46"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E6DFDA"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#DFBA98"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#A58296"));
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
                SetColor(appResources, "ShadowColor", ColorFromHex("#100A12"));
                appResources["ShadowOpacity"] = 0.65;
                break;

            case ThemeMode.Arch:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#111827"));
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#0D1320"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#1B253A"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#1F2937"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#162033"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#28364D"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#DFECF7"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#B5DCEE"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#7B8FA8"));
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
                SetColor(appResources, "ShadowColor", ColorFromHex("#070B12"));
                appResources["ShadowOpacity"] = 0.65;
                break;

            case ThemeMode.Aurora:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#0E122B"));
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#0B0E23"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#1B1F48"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#262252"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#161A3C"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#342D6D"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E3E1F0"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#C9BDF4"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#887FAD"));
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
                SetColor(appResources, "ShadowColor", ColorFromHex("#080A1A"));
                appResources["ShadowOpacity"] = 0.70;
                break;

            case ThemeMode.Cyberpunk:
                SetBrush(appResources, "WindowBgBrush", ColorFromHex("#0D0B18"));
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#0A0813"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#1E1734"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#F43F5E"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#17132B"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#E11D48"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E8E3ED"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#CBD5F2"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#9B92BF"));
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
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#1F242D"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#2F3644"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#434C5E"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#2E3440"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#4C566A"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E2E7F0"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#C8D0DF"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#8694AD"));
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
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#E2E7F0"));
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
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#1C1D26"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#2D2F3D"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#44475A"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#282A36"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#6272A4"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E3E1F0"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#BDA5EC"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#7B88B8"));
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
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#161722"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#24283B"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#2F3549"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#202336"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#3D4566"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#C9D3F2"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#9AA5CE"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#6C76A3"));
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
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#09130F"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#152A22"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#1B382B"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#13261F"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#264D3B"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#DCF0E6"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#9DE0BE"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#65917D"));
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
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#130E15"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#271A2B"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#3A2337"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#231726"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#4A2E46"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E6DFDA"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#F2BCC5"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#9D7B95"));
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
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#1E1E1E"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#323232"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#414141"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#303030"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#4A4A4A"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E4E3E0"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#C9C7C3"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#9A9893"));
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
                SetBrush(appResources, "BadgeForegroundBrush", ColorFromHex("#E4E3E0"));
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
                SetBrush(appResources, "SidebarBgBrush", ColorFromHex("#0B0E17"));
                SetBrush(appResources, "SurfaceElevatedBrush", ColorFromHex("#1B2133"));
                SetBrush(appResources, "WindowBorderBrush", ColorFromHex("#252D40"));
                SetBrush(appResources, "SearchBarBgBrush", ColorFromHex("#161A28"));
                SetBrush(appResources, "SearchBarBorderBrush", ColorFromHex("#2C344A"));
                SetBrush(appResources, "TextPrimaryBrush", ColorFromHex("#E2E8F0"));
                SetBrush(appResources, "TextSecondaryBrush", ColorFromHex("#BAC6D9"));
                SetBrush(appResources, "TextMutedBrush", ColorFromHex("#8492A6"));
                SetBrush(appResources, "IconMutedBrush", ColorFromHex("#94A3B8"));
                SetBrush(appResources, "AccentBrush", ColorFromHex("#3B82F6"));
                SetBrush(appResources, "LogoBrush", ColorFromHex("#60A5FA"));
                SetBrush(appResources, "ItemHoverBgBrush", ColorFromHex("#22293C"));
                SetBrush(appResources, "ItemSelectedBgBrush", Color.FromArgb(48, 59, 130, 246));
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

        // Universal luminous soft-white IconBrush & control tokens on Dark themes (never dull/black or pure glare)
        SetBrush(appResources, "IconBrush", ColorFromHex("#E2E8F0"));
        SetBrush(appResources, "KbdBgBrush", ColorFromHex("#222836"));
        SetBrush(appResources, "KbdBorderBrush", ColorFromHex("#3A4359"));
        SetBrush(appResources, "KbdForegroundBrush", ColorFromHex("#DCE4F0"));
        SetBrush(appResources, "EmptyStateBrush", ColorFromHex("#7B8BA3"));
        SetBrush(appResources, "ToggleOffBgBrush", ColorFromHex("#2D3548"));
        SetBrush(appResources, "ToggleThumbBrush", ColorFromHex("#E2E8F0"));
        SetBrush(appResources, "SubtleHoverBgBrush", Color.FromArgb(24, 255, 255, 255));
        SetBrush(appResources, "GlassRimBrush", Color.FromArgb(42, 255, 255, 255));
    }

    private static void ApplyBaseThemeLightPalette(ResourceDictionary appResources, ThemeMode theme)
    {
        var spec = GetLightPaletteSpec(theme);

        Color winTint = ColorFromHex(spec.WindowTintHex);
        Color sidebarBg = ColorFromHex(spec.SidebarTintHex);
        Color cardBg = ColorFromHex(spec.CardTintHex);
        Color accentColor = ColorFromHex(spec.AccentHex);
        Color iconInk = ColorFromHex(spec.IconInkHex);
        Color primaryInk = ColorFromHex(spec.PrimaryInkHex);

        Color secondaryInk = BlendColors(primaryInk, accentColor, 0.28);
        Color mutedInk = BlendColors(ColorFromHex("#52647D"), accentColor, 0.22);
        Color cardBorder = BlendColors(sidebarBg, accentColor, 0.28);
        Color winBorder = BlendColors(sidebarBg, accentColor, 0.36);
        Color searchBg = BlendColors(ColorFromHex("#FFFFFF"), winTint, 0.38);
        Color searchBorder = BlendColors(cardBorder, accentColor, 0.25);
        Color hoverBg = BlendColors(sidebarBg, accentColor, 0.14);
        Color elevatedBg = BlendColors(cardBg, accentColor, 0.10);
        Color badgeBg = Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B);
        Color badgeBorder = Color.FromArgb(95, accentColor.R, accentColor.G, accentColor.B);

        if (theme == ThemeMode.WindowsVista && spec.VistaGradTop != null && spec.VistaGradMid != null && spec.VistaGradBot != null)
        {
            appResources["WindowBgBrush"] = CreateAeroGradientBrush(
                ColorFromHex(spec.VistaGradTop),
                ColorFromHex(spec.VistaGradMid),
                ColorFromHex(spec.VistaGradBot));
        }
        else
        {
            SetBrush(appResources, "WindowBgBrush", winTint);
        }

        SetBrush(appResources, "SidebarBgBrush", sidebarBg);
        SetBrush(appResources, "SurfaceElevatedBrush", elevatedBg);
        SetBrush(appResources, "WindowBorderBrush", winBorder);
        SetBrush(appResources, "SearchBarBgBrush", searchBg);
        SetBrush(appResources, "SearchBarBorderBrush", searchBorder);
        SetBrush(appResources, "TextPrimaryBrush", primaryInk);
        SetBrush(appResources, "TextSecondaryBrush", secondaryInk);
        SetBrush(appResources, "TextMutedBrush", mutedInk);
        SetBrush(appResources, "IconBrush", iconInk);
        SetBrush(appResources, "IconMutedBrush", BlendColors(iconInk, ColorFromHex("#64748B"), 0.42));
        SetBrush(appResources, "AccentBrush", accentColor);
        SetBrush(appResources, "LogoBrush", accentColor);
        SetBrush(appResources, "ItemHoverBgBrush", hoverBg);
        SetBrush(appResources, "ItemSelectedBgBrush", Color.FromArgb(44, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "ItemSelectedBorderBrush", accentColor);
        SetBrush(appResources, "ContextMenuBgBrush", cardBg);
        SetBrush(appResources, "ContextMenuBorderBrush", cardBorder);
        SetBrush(appResources, "PopupBgBrush", cardBg);
        SetBrush(appResources, "PopupBorderBrush", cardBorder);
        SetBrush(appResources, "SeparatorBrush", cardBorder);
        SetBrush(appResources, "BadgeBgBrush", badgeBg);
        SetBrush(appResources, "BadgeBorderBrush", badgeBorder);
        SetBrush(appResources, "BadgeForegroundBrush", iconInk);
        SetBrush(appResources, "DateBadgeBgBrush", Color.FromArgb(36, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "DateBadgeBorderBrush", Color.FromArgb(125, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "DateBadgeForegroundBrush", iconInk);
        SetBrush(appResources, "CardBgBrush", cardBg);
        SetBrush(appResources, "CardBorderBrush", cardBorder);
        SetBrush(appResources, "ScrollBarThumbBrush", BlendColors(cardBorder, accentColor, 0.35));
        SetBrush(appResources, "ScrollBarThumbHoverBrush", accentColor);
        SetBrush(appResources, "ScrollBarTrackBrush", hoverBg);
        SetBrush(appResources, "KbdBgBrush", elevatedBg);
        SetBrush(appResources, "KbdBorderBrush", cardBorder);
        SetBrush(appResources, "KbdForegroundBrush", iconInk);
        SetBrush(appResources, "EmptyStateBrush", mutedInk);
        SetBrush(appResources, "ToggleOffBgBrush", BlendColors(cardBorder, mutedInk, 0.25));
        SetBrush(appResources, "ToggleThumbBrush", ColorFromHex("#FFFFFF"));
        SetBrush(appResources, "SubtleHoverBgBrush", Color.FromArgb(20, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "SelectionIndicatorBrush", accentColor);
        SetBrush(appResources, "GlassRimBrush", Color.FromArgb(220, 255, 255, 255));
        SetColor(appResources, "ShadowColor", BlendColors(ColorFromHex("#0F172A"), accentColor, 0.28));
        appResources["ShadowOpacity"] = 0.16;
    }

    private static void ApplyGlassSurface(ResourceDictionary appResources, ThemeMode theme, bool isLight, Color accentColor)
    {
        if (isLight)
        {
            var spec = GetLightPaletteSpec(theme);
            Color baseTint = !string.IsNullOrWhiteSpace(CustomGlassTintHex)
                ? BlendColors(ColorFromHex(CustomGlassTintHex), ColorFromHex("#FFFFFF"), 0.55)
                : ColorFromHex(spec.WindowTintHex);

            if (GlassTintWithAccent)
            {
                baseTint = BlendColors(baseTint, accentColor, 0.18);
            }

            byte alpha = (byte)Math.Clamp((int)(GlassOpacity * 255), 214, 246);

            if (theme == ThemeMode.WindowsVista && string.IsNullOrWhiteSpace(CustomGlassTintHex) && spec.VistaGradTop != null && spec.VistaGradMid != null && spec.VistaGradBot != null)
            {
                Color c1 = ColorFromHex(spec.VistaGradTop);
                Color c2 = ColorFromHex(spec.VistaGradMid);
                Color c3 = ColorFromHex(spec.VistaGradBot);
                appResources["WindowBgBrush"] = CreateAeroGradientBrush(
                    Color.FromArgb(alpha, c1.R, c1.G, c1.B),
                    Color.FromArgb(alpha, c2.R, c2.G, c2.B),
                    Color.FromArgb(alpha, c3.R, c3.G, c3.B));
            }
            else
            {
                SetBrush(appResources, "WindowBgBrush", Color.FromArgb(alpha, baseTint.R, baseTint.G, baseTint.B));
            }

            Color sideColor = BlendColors(baseTint, ColorFromHex(spec.SidebarTintHex), 0.72);
            SetBrush(appResources, "SidebarBgBrush", Color.FromArgb((byte)Math.Min(250, alpha + 8), sideColor.R, sideColor.G, sideColor.B));

            Color cardCol = ColorFromHex(spec.CardTintHex);
            SetBrush(appResources, "CardBgBrush", Color.FromArgb(236, cardCol.R, cardCol.G, cardCol.B));
            SetBrush(appResources, "PopupBgBrush", Color.FromArgb(250, cardCol.R, cardCol.G, cardCol.B));
            SetBrush(appResources, "ContextMenuBgBrush", Color.FromArgb(250, cardCol.R, cardCol.G, cardCol.B));

            Color searchCol = BlendColors(ColorFromHex("#FFFFFF"), baseTint, 0.32);
            Color searchBorder = BlendColors(ColorFromHex("#94A3B8"), accentColor, 0.35);
            SetBrush(appResources, "SearchBarBgBrush", Color.FromArgb(238, searchCol.R, searchCol.G, searchCol.B));
            SetBrush(appResources, "SearchBarBorderBrush", Color.FromArgb(190, searchBorder.R, searchBorder.G, searchBorder.B));

            if (ApplyAccentToBorder)
            {
                SetBrush(appResources, "WindowBorderBrush", Color.FromArgb(195, accentColor.R, accentColor.G, accentColor.B));
            }
            else
            {
                Color borderCol = BlendColors(sideColor, accentColor, 0.36);
                SetBrush(appResources, "WindowBorderBrush", Color.FromArgb(220, borderCol.R, borderCol.G, borderCol.B));
            }
        }
        else
        {
            Color tintColor;
            if (GlassTintWithAccent)
            {
                tintColor = BlendColors(ColorFromHex("#090B11"), accentColor, 0.28);
            }
            else if (!string.IsNullOrWhiteSpace(CustomGlassTintHex))
            {
                tintColor = ColorFromHex(CustomGlassTintHex);
            }
            else
            {
                tintColor = ColorFromHex(GetDefaultGlassTintHex(theme));
            }

            byte alpha = (byte)Math.Clamp((int)(GlassOpacity * 255), 75, 248);

            if (theme == ThemeMode.WindowsVista && string.IsNullOrWhiteSpace(CustomGlassTintHex) && !GlassTintWithAccent)
            {
                appResources["WindowBgBrush"] = CreateAeroGradientBrush(
                    Color.FromArgb(alpha, 0x10, 0x2B, 0x42),
                    Color.FromArgb(alpha, 0x0B, 0x1B, 0x2E),
                    Color.FromArgb(alpha, 0x12, 0x36, 0x50));
            }
            else
            {
                SetBrush(appResources, "WindowBgBrush", Color.FromArgb(alpha, tintColor.R, tintColor.G, tintColor.B));
            }

            byte sideAlpha = (byte)Math.Min(250, alpha + 14);
            SetBrush(appResources, "SidebarBgBrush", Color.FromArgb(sideAlpha, (byte)(tintColor.R * 0.82), (byte)(tintColor.G * 0.82), (byte)(tintColor.B * 0.86)));

            byte barAlpha = (byte)Math.Min(245, alpha + 28);
            byte barR = (byte)Math.Min(255, tintColor.R + 18);
            byte barG = (byte)Math.Min(255, tintColor.G + 18);
            byte barB = (byte)Math.Min(255, tintColor.B + 24);
            SetBrush(appResources, "SearchBarBgBrush", Color.FromArgb(barAlpha, barR, barG, barB));
            SetBrush(appResources, "SearchBarBorderBrush", Color.FromArgb(115, (byte)Math.Min(255, barR + 60), (byte)Math.Min(255, barG + 60), (byte)Math.Min(255, barB + 80)));

            if (ApplyAccentToBorder)
            {
                SetBrush(appResources, "WindowBorderBrush", Color.FromArgb(175, accentColor.R, accentColor.G, accentColor.B));
            }
            else
            {
                byte borderR = (byte)Math.Min(255, tintColor.R + 65);
                byte borderG = (byte)Math.Min(255, tintColor.G + 65);
                byte borderB = (byte)Math.Min(255, tintColor.B + 85);
                SetBrush(appResources, "WindowBorderBrush", Color.FromArgb(115, borderR, borderG, borderB));
            }

            byte cardAlpha = (byte)Math.Clamp(alpha - 24, 55, 224);
            SetBrush(appResources, "CardBgBrush", Color.FromArgb(cardAlpha, (byte)Math.Min(255, tintColor.R + 14), (byte)Math.Min(255, tintColor.G + 14), (byte)Math.Min(255, tintColor.B + 18)));
            SetBrush(appResources, "PopupBgBrush", Color.FromArgb(242, tintColor.R, tintColor.G, tintColor.B));
            SetBrush(appResources, "ContextMenuBgBrush", Color.FromArgb(242, tintColor.R, tintColor.G, tintColor.B));
            SetBrush(appResources, "KbdBgBrush", Color.FromArgb(150, (byte)Math.Min(255, tintColor.R + 18), (byte)Math.Min(255, tintColor.G + 18), (byte)Math.Min(255, tintColor.B + 24)));
            SetBrush(appResources, "KbdBorderBrush", Color.FromArgb(110, (byte)Math.Min(255, tintColor.R + 60), (byte)Math.Min(255, tintColor.G + 60), (byte)Math.Min(255, tintColor.B + 80)));
            SetBrush(appResources, "KbdForegroundBrush", ColorFromHex("#DCE4F0"));
            SetBrush(appResources, "SelectionIndicatorBrush", accentColor);
            SetBrush(appResources, "GlassRimBrush", Color.FromArgb(46, 255, 255, 255));
        }

        // Specular sheen highlight (stronger Aero crystal sheen on Windows Vista)
        bool isVista = theme == ThemeMode.WindowsVista;
        var specular = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        byte rimStart = isVista
            ? (isLight ? (byte)245 : (byte)95)
            : (isLight ? (byte)220 : (byte)45);
        byte rimMid = isVista
            ? (isLight ? (byte)110 : (byte)28)
            : (isLight ? (byte)65 : (byte)8);
        specular.GradientStops.Add(new GradientStop(Color.FromArgb(rimStart, 255, 255, 255), 0.0));
        specular.GradientStops.Add(new GradientStop(Color.FromArgb(rimMid, 255, 255, 255), 0.35));
        specular.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
        specular.Freeze();
        appResources["GlassSpecularBrush"] = specular;

        if (ApplyAccentToBorder)
        {
            SetColor(appResources, "ShadowColor", accentColor);
            appResources["ShadowOpacity"] = isLight ? 0.25 : 0.50;
        }
        else
        {
            SetColor(appResources, "ShadowColor", isLight ? BlendColors(ColorFromHex("#0F172A"), accentColor, 0.28) : ColorFromHex("#0F172A"));
            appResources["ShadowOpacity"] = isLight ? 0.16 : 0.65;
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

        byte selectAlpha = isLight ? (byte)36 : (byte)48;
        SetBrush(appResources, "ItemSelectedBgBrush", Color.FromArgb(selectAlpha, accentColor.R, accentColor.G, accentColor.B));

        byte badgeBgAlpha = isLight ? (byte)30 : (byte)40;
        SetBrush(appResources, "DateBadgeBgBrush", Color.FromArgb(badgeBgAlpha, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "DateBadgeBorderBrush", Color.FromArgb(100, accentColor.R, accentColor.G, accentColor.B));
        SetBrush(appResources, "DateBadgeForegroundBrush", isLight ? BlendColors(accentColor, ColorFromHex("#0F172A"), 0.22) : accentColor);

        if (!isGlass && ApplyAccentToBorder)
        {
            SetBrush(appResources, "WindowBorderBrush", accentColor);
        }
    }

    private static LinearGradientBrush CreateAeroGradientBrush(Color top, Color mid, Color bottom)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0.2, 1)
        };
        brush.GradientStops.Add(new GradientStop(top, 0.0));
        brush.GradientStops.Add(new GradientStop(mid, 0.52));
        brush.GradientStops.Add(new GradientStop(bottom, 1.0));
        brush.Freeze();
        return brush;
    }

    public static string GetDefaultGlassTintHex(ThemeMode theme)
    {
        return theme switch
        {
            ThemeMode.Windows11 => "#1B202C",
            ThemeMode.Windows10 => "#141923",
            ThemeMode.WindowsVista => "#0D2438",
            ThemeMode.Ubuntu => "#1E1322",
            ThemeMode.Arch => "#111827",
            ThemeMode.Aurora => "#0E122B",
            ThemeMode.Cyberpunk => "#0D0B18",
            ThemeMode.Nord => "#242933",
            ThemeMode.Dracula => "#21222C",
            ThemeMode.Emerald => "#0C1814",
            ThemeMode.Sunset => "#18111A",
            ThemeMode.Linux => "#242424",
            _ => "#0B132B"
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
            ThemeMode.Windows11 => "#0078D4",
            ThemeMode.Windows10 => "#0078D7",
            ThemeMode.WindowsVista => "#1BA1E2",
            _ => "#3B82F6"
        };
    }

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

        return "#0078D7";
    }

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

    public static double GetRelativeLuminance(Color c)
    {
        static double Linearize(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(c.R) + 0.7152 * Linearize(c.G) + 0.0722 * Linearize(c.B);
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
