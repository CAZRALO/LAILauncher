using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.AI;
using Launcher.App.Services;
using Launcher.Core.Utilities;
using Launcher.Database.Repositories;
using Launcher.Indexer;

namespace Launcher.App;

public sealed record ProcessItemInfo(
    int Pid,
    string Name,
    string Subtitle,
    string MemoryText,
    long WorkingSetBytes,
    string CriticalTag,
    bool IsCritical);

public partial class SettingsWindow : Window
{
    private static readonly HashSet<string> CriticalProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "smss", "csrss", "wininit", "winlogon",
        "services", "lsass", "dwm", "explorer", "svchost", "fontdrvhost",
        "registry", "memory compression"
    };

    private readonly ISettingsRepository _settingsRepo;
    private readonly GlobalHotkey? _hotkey;
    private readonly Action<string>? _onHotkeyUpdated;
    private readonly IAIProvider? _aiProvider;
    private readonly OllamaProcessService? _ollamaProcessService;
    private readonly Action<MonitorTargetMode, string?, WindowVerticalPosition>? _onMonitorSettingsUpdated;
    private readonly Action<string>? _onTestPositionRequested;
    private readonly AppIndexer? _appIndexer;
    private readonly FileIndexer? _fileIndexer;
    private readonly IAppRepository? _appRepo;
    private readonly IFileRepository? _fileRepo;
    private readonly Action<double, double>? _onWindowSizeUpdated;
    private readonly Action<string>? _onMoveWindowPreset;
    private readonly Action? _onBehaviorSettingsUpdated;

    private bool _isLoading = true;
    private bool _isSliderSyncing;
    private List<ProcessItemInfo> _allProcesses = new();
    private List<MonitorItemInfo> _detectedMonitors = new();

    public SettingsWindow(
        ISettingsRepository settingsRepo,
        GlobalHotkey? hotkey = null,
        Action<string>? onHotkeyUpdated = null,
        IAIProvider? aiProvider = null,
        OllamaProcessService? ollamaProcessService = null,
        Action<MonitorTargetMode, string?, WindowVerticalPosition>? onMonitorSettingsUpdated = null,
        Action<string>? onTestPositionRequested = null,
        AppIndexer? appIndexer = null,
        FileIndexer? fileIndexer = null,
        IAppRepository? appRepo = null,
        IFileRepository? fileRepo = null,
        Action<double, double>? onWindowSizeUpdated = null,
        Action<string>? onMoveWindowPreset = null,
        Action? onBehaviorSettingsUpdated = null)
    {
        InitializeComponent();
        _settingsRepo = settingsRepo;
        _hotkey = hotkey;
        _onHotkeyUpdated = onHotkeyUpdated;
        _aiProvider = aiProvider;
        _ollamaProcessService = ollamaProcessService;
        _onMonitorSettingsUpdated = onMonitorSettingsUpdated;
        _onTestPositionRequested = onTestPositionRequested;
        _appIndexer = appIndexer;
        _fileIndexer = fileIndexer;
        _appRepo = appRepo;
        _fileRepo = fileRepo;
        _onWindowSizeUpdated = onWindowSizeUpdated;
        _onMoveWindowPreset = onMoveWindowPreset;
        _onBehaviorSettingsUpdated = onBehaviorSettingsUpdated;

        MaxHeight = SystemParameters.WorkArea.Height + 14;
        MaxWidth = SystemParameters.WorkArea.Width + 14;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.EnableWindowBlur(this);
        SelectNavTab(0);
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _isLoading = true;
        try
        {
            await LoadLanguageAsync();
            await LoadGeneralSettingsAsync();
            LoadAppearanceSettings();
            await LoadWindowAndDisplaySettingsAsync();
            await LoadAppsBehaviorSettingsAsync();
            await LoadAISettingsAsync();
            LoadAboutInfo();
        }
        finally
        {
            _isLoading = false;
        }
    }

    #region Window Chrome & Sidebar Navigation

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnWindowStateChanged(object sender, EventArgs e)
    {
        if (RootWindowBorder != null)
        {
            if (WindowState == WindowState.Maximized)
            {
                RootWindowBorder.Margin = new Thickness(7);
                RootWindowBorder.CornerRadius = new CornerRadius(0);
            }
            else
            {
                RootWindowBorder.Margin = new Thickness(10);
                RootWindowBorder.CornerRadius = TryFindResource("WindowCornerRadius") is CornerRadius cr
                    ? cr
                    : new CornerRadius(14);
            }
        }

        if (SettingsMaxBtn?.Template?.FindName("SettingsMaxIcon", SettingsMaxBtn) is System.Windows.Shapes.Path maxIcon)
        {
            maxIcon.Data = WindowState == WindowState.Maximized
                ? Geometry.Parse("M 3,5 H 9 V 11 H 3 Z M 5,3 H 11 V 9")
                : Geometry.Parse("M 2,2 H 10 V 10 H 2 Z");
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.OriginalSource is DependencyObject dep &&
            (FindVisualParent<Button>(dep) != null ||
             FindVisualParent<ComboBox>(dep) != null ||
             FindVisualParent<TextBox>(dep) != null))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            OnMaximizeRestoreClicked(sender, new RoutedEventArgs());
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnMinimizeClicked(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClicked(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnNavTabClicked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && int.TryParse(rb.Tag?.ToString(), out int tabIndex))
        {
            SelectNavTab(tabIndex);
        }
    }

    private void SelectNavTab(int tabIndex)
    {
        PanelGeneral.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelAppearance.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelWindow.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelApps.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        PanelSystem.Visibility = tabIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        PanelAI.Visibility = tabIndex == 5 ? Visibility.Visible : Visibility.Collapsed;
        PanelAbout.Visibility = tabIndex == 6 ? Visibility.Visible : Visibility.Collapsed;

        NavGeneralRadio.IsChecked = tabIndex == 0;
        NavAppearanceRadio.IsChecked = tabIndex == 1;
        NavWindowRadio.IsChecked = tabIndex == 2;
        NavAppsRadio.IsChecked = tabIndex == 3;
        NavSystemRadio.IsChecked = tabIndex == 4;
        NavAIRadio.IsChecked = tabIndex == 5;
        NavAboutRadio.IsChecked = tabIndex == 6;

        if (tabIndex == 2)
        {
            RefreshMonitorsList();
        }
        else if (tabIndex == 3)
        {
            _ = RefreshAppsAndProcessesAsync();
        }
        else if (tabIndex == 4)
        {
            _ = RefreshWindowsControlsStateAsync();
        }
    }

    private async Task LoadLanguageAsync()
    {
        var savedLang = await _settingsRepo.GetAsync("AppLanguage");
        string lang = string.IsNullOrWhiteSpace(savedLang)
            ? LocalizationService.CurrentLanguage
            : savedLang;

        SelectLanguageComboByTag(lang);
    }

    private void SelectLanguageComboByTag(string lang)
    {
        if (LanguageComboBox == null) return;
        foreach (var item in LanguageComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), lang, StringComparison.OrdinalIgnoreCase))
            {
                LanguageComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private async void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (LanguageComboBox?.SelectedItem is not ComboBoxItem item) return;

        var lang = item.Tag?.ToString() ?? LocalizationService.Vietnamese;
        LocalizationService.SetLanguage(lang);
        await _settingsRepo.SetAsync("AppLanguage", lang);

        LoadAboutInfo();
        RefreshMonitorsList();
        await RefreshSavedPositionStatusAsync();
        await RefreshAppsAndProcessesAsync();
        await RefreshWindowsControlsStateAsync();
    }

    #endregion

    #region Tab 0: General & Hotkey

    private async Task LoadGeneralSettingsAsync()
    {
        var savedHotkey = await _settingsRepo.GetAsync("AppHotkey");
        var currentHotkeyStr = !string.IsNullOrWhiteSpace(savedHotkey)
            ? savedHotkey
            : (_hotkey != null ? GlobalHotkey.FormatHotkey(_hotkey.Modifiers, _hotkey.Key) : "Alt + Space");

        bool matchedPreset = false;
        foreach (var item in HotkeyPresetCombo.Items.OfType<ComboBoxItem>())
        {
            var content = item.Content?.ToString() ?? string.Empty;
            if (content.Contains("Custom", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(content.Replace(" ", ""), currentHotkeyStr.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
            {
                HotkeyPresetCombo.SelectedItem = item;
                matchedPreset = true;
                break;
            }
        }

        if (!matchedPreset)
        {
            var customItem = HotkeyPresetCombo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => (i.Content?.ToString() ?? string.Empty).Contains("Custom", StringComparison.OrdinalIgnoreCase));
            if (customItem != null)
            {
                HotkeyPresetCombo.SelectedItem = customItem;
            }
            CustomHotkeyPanel.Visibility = Visibility.Visible;
            CustomHotkeyBox.Text = currentHotkeyStr;
        }
        else
        {
            CustomHotkeyPanel.Visibility = Visibility.Collapsed;
        }

        HotkeyStatusText.Text = GetHotkeyStatusMessage(currentHotkeyStr);
        AutoStartCheckBox.IsChecked = StartupService.IsStartupEnabled();
    }

    private async void OnHotkeyPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (HotkeyPresetCombo.SelectedItem is not ComboBoxItem item) return;

        var content = item.Content?.ToString() ?? string.Empty;
        if (content.Contains("Custom", StringComparison.OrdinalIgnoreCase))
        {
            CustomHotkeyPanel.Visibility = Visibility.Visible;
            return;
        }

        CustomHotkeyPanel.Visibility = Visibility.Collapsed;
        await ApplyAndSaveHotkeyAsync(content);
    }

    private void OnCustomHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.DeadCharProcessed)
        {
            return;
        }

        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.None)
        {
            HotkeyStatusText.Text = LocalizationService.CurrentLanguage == LocalizationService.English
                ? "Please hold at least Ctrl, Alt, or Shift along with a key."
                : "Vui lòng giữ ít nhất một phím bổ trợ (Ctrl, Alt hoặc Shift) cùng phím chính.";
            return;
        }

        CustomHotkeyBox.Text = GlobalHotkey.FormatHotkey(mods, key);
    }

    private async void OnApplyCustomHotkeyClicked(object sender, RoutedEventArgs e)
    {
        await ApplyAndSaveHotkeyAsync(CustomHotkeyBox.Text);
    }

    private async Task ApplyAndSaveHotkeyAsync(string hotkeyText)
    {
        if (!GlobalHotkey.TryParseHotkey(hotkeyText, out var mods, out var key))
        {
            HotkeyStatusText.Text = LocalizationService.CurrentLanguage == LocalizationService.English
                ? $"Invalid hotkey syntax: {hotkeyText}"
                : $"Tổ hợp phím không hợp lệ: {hotkeyText}";
            return;
        }

        var formatted = GlobalHotkey.FormatHotkey(mods, key);
        bool ok = _hotkey == null || _hotkey.UpdateHotkey(mods, key);
        if (!ok)
        {
            HotkeyStatusText.Text = LocalizationService.CurrentLanguage == LocalizationService.English
                ? $"Failed to register {formatted} (may be reserved by another app)."
                : $"Không thể đăng ký phím tắt {formatted} (có thể đang bị ứng dụng khác chiếm).";
            return;
        }

        await _settingsRepo.SetAsync("AppHotkey", formatted);
        HotkeyStatusText.Text = GetHotkeyStatusMessage(formatted);
        _onHotkeyUpdated?.Invoke(formatted);
    }

    private static string GetHotkeyStatusMessage(string hotkeyText)
    {
        return LocalizationService.CurrentLanguage == LocalizationService.English
            ? $"Active global shortcut: {hotkeyText}"
            : $"Phím tắt đang hoạt động: {hotkeyText}";
    }

    private void OnAutoStartToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        bool enable = AutoStartCheckBox.IsChecked == true;
        if (!StartupService.SetStartup(enable))
        {
            _isLoading = true;
            AutoStartCheckBox.IsChecked = !enable;
            _isLoading = false;
        }
    }

    #endregion

    #region Tab 1: Appearance, Themes, Glass & Accents

    private (RadioButton Radio, ThemeMode Mode)[] GetThemeRadioMap() =>
    [
        (ThemeWin11Radio, ThemeMode.Windows11),
        (ThemeWin10Radio, ThemeMode.Windows10),
        (ThemeVistaRadio, ThemeMode.WindowsVista),
        (ThemeSystemRadio, ThemeMode.System),
        (ThemeUbuntuRadio, ThemeMode.Ubuntu),
        (ThemeArchRadio, ThemeMode.Arch),
        (ThemeAuroraRadio, ThemeMode.Aurora),
        (ThemeCyberpunkRadio, ThemeMode.Cyberpunk),
        (ThemeNordRadio, ThemeMode.Nord),
        (ThemeDraculaRadio, ThemeMode.Dracula),
        (ThemeEmeraldRadio, ThemeMode.Emerald),
        (ThemeSunsetRadio, ThemeMode.Sunset)
    ];

    private void LoadAppearanceSettings()
    {
        OsStyleWindowsRadio.IsChecked = ThemeManager.CurrentOsStyle == OsStyle.Windows;
        OsStyleLinuxRadio.IsChecked = ThemeManager.CurrentOsStyle == OsStyle.Linux;

        ModeForceDarkRadio.IsChecked = !ThemeManager.IsLightMode;
        ModeForceLightRadio.IsChecked = ThemeManager.IsLightMode;

        UiStyleModernRadio.IsChecked = ThemeManager.CurrentUiStyle == UiStyle.ModernGlass;
        UiStyleCompactRadio.IsChecked = ThemeManager.CurrentUiStyle == UiStyle.CompactCommand;
        UiStyleSharpRadio.IsChecked = ThemeManager.CurrentUiStyle == UiStyle.SharpCyber;
        UiStylePillRadio.IsChecked = ThemeManager.CurrentUiStyle == UiStyle.PillFloating;
        UiStyleClassicRadio.IsChecked = ThemeManager.CurrentUiStyle == UiStyle.ClassicCard;

        SyncThemeSelectionControls();
    }

    private void SyncThemeSelectionControls()
    {
        bool prevLoading = _isLoading;
        _isLoading = true;
        try
        {
            ModeForceDarkRadio.IsChecked = !ThemeManager.IsLightMode;
            ModeForceLightRadio.IsChecked = ThemeManager.IsLightMode;

            foreach (var (radio, mode) in GetThemeRadioMap())
            {
                radio.IsChecked = ThemeManager.CurrentTheme == mode;
            }

            EnableGlassCheckBox.IsChecked = ThemeManager.IsGlassEffectEnabled;
            GlassOptionsPanel.IsEnabled = ThemeManager.IsGlassEffectEnabled;
            GlassOptionsPanel.Opacity = ThemeManager.IsGlassEffectEnabled ? 1.0 : 0.5;

            int opacityPct = (int)Math.Round(ThemeManager.GlassOpacity * 100);
            GlassOpacitySlider.Value = Math.Clamp(opacityPct, 35, 100);
            GlassOpacityText.Text = $"{(int)GlassOpacitySlider.Value}%";

            GlassTintWithAccentCheckBox.IsChecked = ThemeManager.GlassTintWithAccent;
            ManualGlassTintPanel.IsEnabled = !ThemeManager.GlassTintWithAccent;
            ManualGlassTintPanel.Opacity = ThemeManager.GlassTintWithAccent ? 0.5 : 1.0;

            var tintHex = ThemeManager.ActiveGlassTintHex;
            CustomGlassTintTextBox.Text = tintHex;
            UpdatePreviewSwatch(GlassTintPreviewSwatch, tintHex);

            UseWindowsAccentCheckBox.IsChecked = ThemeManager.UseWindowsAccentColor;
            AccentOnBorderCheckBox.IsChecked = ThemeManager.ApplyAccentToBorder;
            AccentPalettePanel.IsEnabled = !ThemeManager.UseWindowsAccentColor;
            AccentPalettePanel.Opacity = ThemeManager.UseWindowsAccentColor ? 0.5 : 1.0;

            if (ThemeManager.UseWindowsAccentColor)
            {
                WindowsAccentHintText.Text = LocalizationService.CurrentLanguage == LocalizationService.English
                    ? $"Synced Windows accent: {WindowsControlService.GetWindowsAccentColorHex()}"
                    : $"Đang đồng bộ màu nhấn Windows: {WindowsControlService.GetWindowsAccentColorHex()}";
                WindowsAccentHintText.Visibility = Visibility.Visible;
            }
            else
            {
                WindowsAccentHintText.Visibility = Visibility.Collapsed;
            }

            var accentHex = ThemeManager.ActiveAccentHex;
            CustomAccentTextBox.Text = accentHex;
            UpdatePreviewSwatch(AccentColorPreviewSwatch, accentHex);
        }
        finally
        {
            _isLoading = prevLoading;
        }
    }

    private static void UpdatePreviewSwatch(Border? border, string hex)
    {
        if (border == null) return;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            border.Background = new SolidColorBrush(color);
        }
        catch
        {
        }
    }

    private void ApplyCurrentThemeConfiguration(
        ThemeMode? theme = null,
        string? accentHex = null,
        bool? useWindowsAccent = null,
        bool? accentOnBorder = null,
        bool? glassEnabled = null,
        string? glassTintHex = null,
        double? glassOpacity = null,
        bool? glassTintWithAccent = null,
        OsStyle? osStyle = null,
        bool? forcedLightMode = null,
        UiStyle? uiStyle = null)
    {
        ThemeManager.LoadConfiguration(
            theme: theme ?? ThemeManager.CurrentTheme,
            accentHex: accentHex ?? ThemeManager.CustomAccentColorHex,
            useWindowsAccent: useWindowsAccent ?? ThemeManager.UseWindowsAccentColor,
            accentOnBorder: accentOnBorder ?? ThemeManager.ApplyAccentToBorder,
            glassEnabled: glassEnabled ?? ThemeManager.IsGlassEffectEnabled,
            glassTintHex: glassTintHex ?? ThemeManager.CustomGlassTintHex,
            glassOpacity: glassOpacity ?? ThemeManager.GlassOpacity,
            glassTintWithAccent: glassTintWithAccent ?? ThemeManager.GlassTintWithAccent,
            osStyle: osStyle ?? ThemeManager.CurrentOsStyle,
            forcedLightMode: forcedLightMode ?? ThemeManager.IsLightMode,
            uiStyle: uiStyle ?? ThemeManager.CurrentUiStyle);
        ThemeManager.EnableWindowBlur(this);
    }

    private async void OnOsStyleSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var os = OsStyleLinuxRadio.IsChecked == true ? OsStyle.Linux : OsStyle.Windows;
        ThemeManager.SetOsStyle(os);
        await _settingsRepo.SetAsync("AppOsStyle", os.ToString());
        await _settingsRepo.SetAsync("AppTheme", ThemeManager.CurrentTheme.ToString());
        SyncThemeSelectionControls();
    }

    private async void OnDualModeSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        bool isLight = ModeForceLightRadio.IsChecked == true;
        ThemeManager.SetLightMode(isLight);
        await _settingsRepo.SetAsync("AppForcedLightMode", isLight.ToString());
        SyncThemeSelectionControls();
    }

    private async void OnUiStyleSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        UiStyle style = UiStyle.ModernGlass;
        if (UiStyleCompactRadio.IsChecked == true) style = UiStyle.CompactCommand;
        else if (UiStyleSharpRadio.IsChecked == true) style = UiStyle.SharpCyber;
        else if (UiStylePillRadio.IsChecked == true) style = UiStyle.PillFloating;
        else if (UiStyleClassicRadio.IsChecked == true) style = UiStyle.ClassicCard;

        ApplyCurrentThemeConfiguration(uiStyle: style);
        await _settingsRepo.SetAsync("AppUiStyle", style.ToString());
    }

    private async void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var selected = GetThemeRadioMap().FirstOrDefault(x => x.Radio == sender);
        if (selected.Radio == null) return;

        ThemeManager.ApplyThemePreset(selected.Mode);
        ThemeManager.EnableWindowBlur(this);
        await _settingsRepo.SetAsync("AppTheme", selected.Mode.ToString());
        await _settingsRepo.SetAsync("AppAccentColor", string.Empty);
        await _settingsRepo.SetAsync("AppGlassTintColor", string.Empty);
        await _settingsRepo.SetAsync("AppAccentFromWindows", ThemeManager.UseWindowsAccentColor.ToString());
        await _settingsRepo.SetAsync("AppForcedLightMode", ThemeManager.ForcedLightMode?.ToString() ?? string.Empty);
        SyncThemeSelectionControls();
    }

    private async void OnEnableGlassChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        bool enabled = EnableGlassCheckBox.IsChecked == true;
        ApplyCurrentThemeConfiguration(glassEnabled: enabled);
        await _settingsRepo.SetAsync("AppGlassEnabled", enabled.ToString());
        SyncThemeSelectionControls();
    }

    private async void OnGlassOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (GlassOpacityText != null)
        {
            GlassOpacityText.Text = $"{(int)Math.Round(e.NewValue)}%";
        }

        if (_isLoading) return;
        double opacity = Math.Clamp(e.NewValue / 100.0, 0.35, 1.0);
        ApplyCurrentThemeConfiguration(glassOpacity: opacity);
        await _settingsRepo.SetAsync("AppGlassOpacity", opacity.ToString("F2", CultureInfo.InvariantCulture));
    }

    private async void OnGlassTintWithAccentChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        bool withAccent = GlassTintWithAccentCheckBox.IsChecked == true;
        ApplyCurrentThemeConfiguration(glassTintWithAccent: withAccent);
        await _settingsRepo.SetAsync("AppGlassTintWithAccent", withAccent.ToString());
        SyncThemeSelectionControls();
    }

    private void OnCustomGlassTintTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreviewSwatch(GlassTintPreviewSwatch, CustomGlassTintTextBox.Text);
    }

    private async void OnPresetGlassTintClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            CustomGlassTintTextBox.Text = hex;
            await ApplyCustomGlassTintAsync(hex);
        }
    }

    private async void OnApplyGlassTintClicked(object sender, RoutedEventArgs e)
    {
        await ApplyCustomGlassTintAsync(CustomGlassTintTextBox.Text);
    }

    private async void OnResetGlassTintClicked(object sender, RoutedEventArgs e)
    {
        ThemeManager.SetGlassTint(null);
        ThemeManager.SetGlassTintWithAccent(false);
        await _settingsRepo.SetAsync("AppGlassTintColor", string.Empty);
        await _settingsRepo.SetAsync("AppGlassTintWithAccent", "false");
        SyncThemeSelectionControls();
    }

    private async Task ApplyCustomGlassTintAsync(string rawHex)
    {
        if (!TryNormalizeHex(rawHex, out var normalized)) return;
        ApplyCurrentThemeConfiguration(glassTintHex: normalized, glassTintWithAccent: false);
        await _settingsRepo.SetAsync("AppGlassTintColor", normalized);
        await _settingsRepo.SetAsync("AppGlassTintWithAccent", "false");
        SyncThemeSelectionControls();
    }

    private async void OnUseWindowsAccentChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        bool useWin = UseWindowsAccentCheckBox.IsChecked == true;
        ApplyCurrentThemeConfiguration(useWindowsAccent: useWin);
        await _settingsRepo.SetAsync("AppAccentFromWindows", useWin.ToString());
        SyncThemeSelectionControls();
    }

    private async void OnAccentOnBorderChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        bool onBorder = AccentOnBorderCheckBox.IsChecked == true;
        ApplyCurrentThemeConfiguration(accentOnBorder: onBorder);
        await _settingsRepo.SetAsync("AppAccentOnBorder", onBorder.ToString());
    }

    private void OnCustomAccentTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreviewSwatch(AccentColorPreviewSwatch, CustomAccentTextBox.Text);
    }

    private async void OnPresetAccentClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            CustomAccentTextBox.Text = hex;
            await ApplyCustomAccentAsync(hex);
        }
    }

    private async void OnApplyAccentClicked(object sender, RoutedEventArgs e)
    {
        await ApplyCustomAccentAsync(CustomAccentTextBox.Text);
    }

    private async void OnResetAccentClicked(object sender, RoutedEventArgs e)
    {
        ThemeManager.ApplyCustomAccentColor(null);
        await _settingsRepo.SetAsync("AppAccentColor", string.Empty);
        await _settingsRepo.SetAsync("AppAccentFromWindows", "false");
        SyncThemeSelectionControls();
    }

    private async Task ApplyCustomAccentAsync(string rawHex)
    {
        if (!TryNormalizeHex(rawHex, out var normalized)) return;
        ApplyCurrentThemeConfiguration(accentHex: normalized, useWindowsAccent: false);
        await _settingsRepo.SetAsync("AppAccentColor", normalized);
        await _settingsRepo.SetAsync("AppAccentFromWindows", "false");
        SyncThemeSelectionControls();
    }

    private static bool TryNormalizeHex(string input, out string normalized)
    {
        normalized = input.Trim();
        if (!normalized.StartsWith('#'))
        {
            normalized = "#" + normalized;
        }

        try
        {
            _ = (Color)ColorConverter.ConvertFromString(normalized);
            normalized = normalized.ToUpperInvariant();
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Tab 2: Window Size, Position & Multi-Display

    private async Task LoadWindowAndDisplaySettingsAsync()
    {
        _isSliderSyncing = true;
        try
        {
            var savedW = await _settingsRepo.GetAsync("AppWindowWidth");
            var savedH = await _settingsRepo.GetAsync("AppWindowHeight");
            double w = double.TryParse(savedW, NumberStyles.Float, CultureInfo.InvariantCulture, out var pw) ? Math.Clamp(pw, 520, 1120) : 680;
            double h = double.TryParse(savedH, NumberStyles.Float, CultureInfo.InvariantCulture, out var ph) ? Math.Clamp(ph, 340, 820) : 460;

            LauncherWidthSlider.Value = w;
            LauncherHeightSlider.Value = h;
            LauncherWidthText.Text = $"{(int)Math.Round(w)} px";
            LauncherHeightText.Text = $"{(int)Math.Round(h)} px";
        }
        finally
        {
            _isSliderSyncing = false;
        }

        RefreshMonitorsList();

        var savedMonMode = await _settingsRepo.GetAsync("AppMonitorTargetMode");
        var monMode = Enum.TryParse<MonitorTargetMode>(savedMonMode, true, out var pm) ? pm : MonitorTargetMode.Cursor;
        MonitorModeCursorRadio.IsChecked = monMode == MonitorTargetMode.Cursor;
        MonitorModePrimaryRadio.IsChecked = monMode == MonitorTargetMode.Primary;
        MonitorModeActiveWinRadio.IsChecked = monMode == MonitorTargetMode.ActiveWindow;
        MonitorModeSpecificRadio.IsChecked = monMode == MonitorTargetMode.Specific;
        SpecificMonitorPanel.Visibility = monMode == MonitorTargetMode.Specific ? Visibility.Visible : Visibility.Collapsed;

        var specificDevice = await _settingsRepo.GetAsync("AppMonitorSpecificDevice");
        if (!string.IsNullOrEmpty(specificDevice))
        {
            foreach (var item in SpecificMonitorCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), specificDevice, StringComparison.OrdinalIgnoreCase))
                {
                    SpecificMonitorCombo.SelectedItem = item;
                    break;
                }
            }
        }

        var savedPosMode = await _settingsRepo.GetAsync("AppWindowPositionMode");
        var posMode = Enum.TryParse<WindowVerticalPosition>(savedPosMode, true, out var vp) ? vp : WindowVerticalPosition.TopCenter;
        PositionTopCenterRadio.IsChecked = posMode == WindowVerticalPosition.TopCenter;
        PositionCenterRadio.IsChecked = posMode == WindowVerticalPosition.Center;

        await RefreshSavedPositionStatusAsync();
    }

    private async Task RefreshSavedPositionStatusAsync()
    {
        var enabledStr = await _settingsRepo.GetAsync("AppWindowCustomPositionEnabled");
        bool hasCustom = bool.TryParse(enabledStr, out var b) && b;
        var xStr = await _settingsRepo.GetAsync("AppWindowCustomPositionX");
        var yStr = await _settingsRepo.GetAsync("AppWindowCustomPositionY");

        if (hasCustom &&
            double.TryParse(xStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var px) &&
            double.TryParse(yStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var py))
        {
            SavedPositionStatusText.Text = LocalizationService.CurrentLanguage == LocalizationService.English
                ? $"Custom coordinate: X = {(int)px}, Y = {(int)py}"
                : $"Tọa độ tùy chỉnh: X = {(int)px}, Y = {(int)py}";
            ResetPositionSettingsBtn.IsEnabled = true;
        }
        else
        {
            SavedPositionStatusText.Text = LocalizationService.CurrentLanguage == LocalizationService.English
                ? "Auto-aligned to monitor anchor preset"
                : "Đang tự động căn theo vị trí chuẩn màn hình";
            ResetPositionSettingsBtn.IsEnabled = false;
        }
    }

    private async void OnLauncherSizeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LauncherWidthSlider == null || LauncherHeightSlider == null) return;
        double w = Math.Round(LauncherWidthSlider.Value);
        double h = Math.Round(LauncherHeightSlider.Value);
        LauncherWidthText.Text = $"{(int)w} px";
        LauncherHeightText.Text = $"{(int)h} px";

        if (_isLoading || _isSliderSyncing) return;

        await _settingsRepo.SetAsync("AppWindowWidth", ((int)w).ToString(CultureInfo.InvariantCulture));
        await _settingsRepo.SetAsync("AppWindowHeight", ((int)h).ToString(CultureInfo.InvariantCulture));
        _onWindowSizeUpdated?.Invoke(w, h);
    }

    private async void OnSizePresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string raw) return;
        var parts = raw.Split('x', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
        {
            return;
        }

        _isSliderSyncing = true;
        try
        {
            LauncherWidthSlider.Value = Math.Clamp(w, 560, 1120);
            LauncherHeightSlider.Value = Math.Clamp(h, 380, 820);
            LauncherWidthText.Text = $"{(int)LauncherWidthSlider.Value} px";
            LauncherHeightText.Text = $"{(int)LauncherHeightSlider.Value} px";
        }
        finally
        {
            _isSliderSyncing = false;
        }

        await _settingsRepo.SetAsync("AppWindowWidth", ((int)LauncherWidthSlider.Value).ToString(CultureInfo.InvariantCulture));
        await _settingsRepo.SetAsync("AppWindowHeight", ((int)LauncherHeightSlider.Value).ToString(CultureInfo.InvariantCulture));
        _onWindowSizeUpdated?.Invoke(LauncherWidthSlider.Value, LauncherHeightSlider.Value);
    }

    private async void OnWindowPositionPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string preset) return;
        _onMoveWindowPreset?.Invoke(preset);

        if (preset == "Center")
        {
            _isLoading = true;
            PositionCenterRadio.IsChecked = true;
            _isLoading = false;
        }
        else if (preset == "TopCenter")
        {
            _isLoading = true;
            PositionTopCenterRadio.IsChecked = true;
            _isLoading = false;
        }

        await Task.Delay(80);
        await RefreshSavedPositionStatusAsync();
    }

    private async void OnResetPositionSettingsClicked(object sender, RoutedEventArgs e)
    {
        await _settingsRepo.SetAsync("AppWindowCustomPositionEnabled", "false");
        _onMoveWindowPreset?.Invoke(PositionCenterRadio.IsChecked == true ? "Center" : "TopCenter");
        await RefreshSavedPositionStatusAsync();
    }

    private async void OnMonitorModeSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var mode = MonitorTargetMode.Cursor;
        if (MonitorModePrimaryRadio.IsChecked == true) mode = MonitorTargetMode.Primary;
        else if (MonitorModeActiveWinRadio.IsChecked == true) mode = MonitorTargetMode.ActiveWindow;
        else if (MonitorModeSpecificRadio.IsChecked == true) mode = MonitorTargetMode.Specific;

        SpecificMonitorPanel.Visibility = mode == MonitorTargetMode.Specific ? Visibility.Visible : Visibility.Collapsed;
        await _settingsRepo.SetAsync("AppMonitorTargetMode", mode.ToString());
        NotifyMonitorSettingsChanged();
    }

    private async void OnSpecificMonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (SpecificMonitorCombo.SelectedItem is ComboBoxItem item && item.Tag is string dev)
        {
            await _settingsRepo.SetAsync("AppMonitorSpecificDevice", dev);
            NotifyMonitorSettingsChanged();
        }
    }

    private async void OnWindowPositionChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var vPos = PositionCenterRadio.IsChecked == true
            ? WindowVerticalPosition.Center
            : WindowVerticalPosition.TopCenter;

        await _settingsRepo.SetAsync("AppWindowPositionMode", vPos.ToString());
        NotifyMonitorSettingsChanged();
    }

    private void NotifyMonitorSettingsChanged()
    {
        var mode = MonitorTargetMode.Cursor;
        if (MonitorModePrimaryRadio.IsChecked == true) mode = MonitorTargetMode.Primary;
        else if (MonitorModeActiveWinRadio.IsChecked == true) mode = MonitorTargetMode.ActiveWindow;
        else if (MonitorModeSpecificRadio.IsChecked == true) mode = MonitorTargetMode.Specific;

        var specificDev = (SpecificMonitorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var vPos = PositionCenterRadio.IsChecked == true
            ? WindowVerticalPosition.Center
            : WindowVerticalPosition.TopCenter;

        _onMonitorSettingsUpdated?.Invoke(mode, specificDev, vPos);
    }

    private void OnRefreshMonitorsClicked(object sender, RoutedEventArgs e)
    {
        RefreshMonitorsList();
    }

    private void RefreshMonitorsList()
    {
        _detectedMonitors = MonitorService.GetMonitors();
        if (MonitorsCountBadge != null)
        {
            MonitorsCountBadge.Text = _detectedMonitors.Count.ToString(CultureInfo.InvariantCulture);
        }

        if (SpecificMonitorCombo != null)
        {
            var prevTag = (SpecificMonitorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool prevLoading = _isLoading;
            _isLoading = true;
            SpecificMonitorCombo.Items.Clear();
            foreach (var mon in _detectedMonitors)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{mon.DisplayName} ({mon.ResolutionText})",
                    Tag = mon.DeviceName
                };
                SpecificMonitorCombo.Items.Add(item);
                if (string.Equals(mon.DeviceName, prevTag, StringComparison.OrdinalIgnoreCase))
                {
                    SpecificMonitorCombo.SelectedItem = item;
                }
            }

            if (SpecificMonitorCombo.SelectedIndex < 0 && SpecificMonitorCombo.Items.Count > 0)
            {
                SpecificMonitorCombo.SelectedIndex = 0;
            }
            _isLoading = prevLoading;
        }

        if (MonitorsCardsContainer == null) return;
        MonitorsCardsContainer.Children.Clear();

        foreach (var mon in _detectedMonitors)
        {
            var card = new Border
            {
                Background = TryFindResource("SurfaceElevatedBrush") as Brush ?? Brushes.Transparent,
                BorderBrush = TryFindResource("CardBorderBrush") as Brush ?? Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel();
            var primarySuffix = mon.IsPrimary
                ? (LocalizationService.CurrentLanguage == LocalizationService.English ? " • Primary" : " • Màn hình chính")
                : "";
            infoStack.Children.Add(new TextBlock
            {
                Text = $"{mon.DisplayName}{primarySuffix}",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.White
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = $"{mon.ResolutionText} • DPI {mon.DpiPercentText} • {mon.DeviceName}",
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.LightGray
            });
            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            var testBtn = new Button
            {
                Content = LocalizationService.CurrentLanguage == LocalizationService.English ? "Preview Here" : "Thử trên màn hình này",
                Style = TryFindResource("SubtleBtnStyle") as Style,
                Tag = mon.DeviceName,
                VerticalAlignment = VerticalAlignment.Center
            };
            testBtn.Click += (s, args) =>
            {
                if (s is Button b && b.Tag is string devName)
                {
                    _onTestPositionRequested?.Invoke(devName);
                }
            };
            Grid.SetColumn(testBtn, 1);
            grid.Children.Add(testBtn);

            card.Child = grid;
            MonitorsCardsContainer.Children.Add(card);
        }
    }

    #endregion

    #region Tab 3: Apps, Tray Behavior & Background Processes

    private async Task LoadAppsBehaviorSettingsAsync()
    {
        ShowTrayIconCheckBox.IsChecked = await GetBoolSettingAsync("AppShowTrayIcon", defaultValue: true);
        MinimizeToTrayCheckBox.IsChecked = await GetBoolSettingAsync("AppMinimizeToTray", defaultValue: true);
        HideOnDeactivateCheckBox.IsChecked = await GetBoolSettingAsync("AppHideOnDeactivate", defaultValue: true);
        RememberLastQueryCheckBox.IsChecked = await GetBoolSettingAsync("AppRememberLastQuery", defaultValue: false);
        ShowQuickStripCheckBox.IsChecked = await GetBoolSettingAsync("AppShowQuickStrip", defaultValue: false);

        await RefreshIndexCountsAsync();
    }

    private async Task<bool> GetBoolSettingAsync(string key, bool defaultValue)
    {
        var raw = await _settingsRepo.GetAsync(key);
        return string.IsNullOrWhiteSpace(raw)
            ? defaultValue
            : (bool.TryParse(raw, out var val) ? val : defaultValue);
    }

    private async void OnAppBehaviorToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender is not CheckBox cb) return;

        string? settingKey = cb == ShowTrayIconCheckBox ? "AppShowTrayIcon"
            : cb == MinimizeToTrayCheckBox ? "AppMinimizeToTray"
            : cb == HideOnDeactivateCheckBox ? "AppHideOnDeactivate"
            : cb == RememberLastQueryCheckBox ? "AppRememberLastQuery"
            : cb == ShowQuickStripCheckBox ? "AppShowQuickStrip"
            : null;
        if (settingKey == null) return;

        bool value = cb.IsChecked == true;
        await _settingsRepo.SetAsync(settingKey, value.ToString());

        if (string.Equals(settingKey, "AppShowTrayIcon", StringComparison.Ordinal))
        {
            (Application.Current as App)?.SetTrayIconVisible(value);
        }
        else
        {
            _onBehaviorSettingsUpdated?.Invoke();
        }
    }

    private async Task RefreshAppsAndProcessesAsync()
    {
        await RefreshIndexCountsAsync();
        await RefreshRunningProcessesAsync();
    }

    private async Task RefreshIndexCountsAsync()
    {
        try
        {
            int appCount = _appRepo != null ? (await _appRepo.GetAllAsync()).Count() : 0;
            int fileCount = _fileRepo != null ? (await _fileRepo.GetAllAsync()).Count() : 0;

            bool isEn = LocalizationService.CurrentLanguage == LocalizationService.English;
            if (IndexedAppsCountText != null)
            {
                IndexedAppsCountText.Text = isEn ? $"{appCount} indexed apps" : $"{appCount} ứng dụng";
            }
            if (IndexedFilesCountText != null)
            {
                IndexedFilesCountText.Text = isEn ? $"{fileCount} indexed files" : $"{fileCount} tệp tin";
            }
        }
        catch
        {
        }
    }

    private async void OnReindexAppsClicked(object sender, RoutedEventArgs e)
    {
        if (_appIndexer == null) return;
        ReindexAppsButton.IsEnabled = false;
        try
        {
            await _appIndexer.ScanAllAppsAsync();
            await RefreshIndexCountsAsync();
        }
        finally
        {
            ReindexAppsButton.IsEnabled = true;
        }
    }

    private async void OnReindexFilesClicked(object sender, RoutedEventArgs e)
    {
        if (_fileIndexer == null) return;
        ReindexFilesButton.IsEnabled = false;
        try
        {
            await _fileIndexer.ScanUserDirectoriesAsync();
            await RefreshIndexCountsAsync();
        }
        finally
        {
            ReindexFilesButton.IsEnabled = true;
        }
    }

    private async void OnRefreshProcessesClicked(object sender, RoutedEventArgs e)
    {
        await RefreshRunningProcessesAsync();
    }

    private void OnProcessFilterChanged(object sender, TextChangedEventArgs e)
    {
        ApplyProcessFilter();
    }

    private async Task RefreshRunningProcessesAsync()
    {
        var currentPid = Environment.ProcessId;
        var list = await Task.Run(() =>
        {
            var items = new List<ProcessItemInfo>();
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    long memBytes = proc.WorkingSet64;
                    double mb = memBytes / (1024.0 * 1024.0);
                    bool isCritical = CriticalProcessNames.Contains(proc.ProcessName) || proc.Id == currentPid;
                    string tag = isCritical ? "Protected" : "App";

                    items.Add(new ProcessItemInfo(
                        Pid: proc.Id,
                        Name: proc.ProcessName,
                        Subtitle: $"PID {proc.Id} • {(string.IsNullOrWhiteSpace(proc.MainWindowTitle) ? "Background" : proc.MainWindowTitle)}",
                        MemoryText: $"{mb:F0} MB",
                        WorkingSetBytes: memBytes,
                        CriticalTag: tag,
                        IsCritical: isCritical));
                }
                catch
                {
                }
                finally
                {
                    proc.Dispose();
                }
            }

            return items
                .OrderByDescending(p => p.WorkingSetBytes)
                .Take(120)
                .ToList();
        });

        _allProcesses = list;
        ApplyProcessFilter();
    }

    private void ApplyProcessFilter()
    {
        if (ProcessesListBox == null || ProcessSummaryText == null) return;
        var filter = ProcessFilterTextBox?.Text?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allProcesses
            : _allProcesses.Where(p =>
                p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString(CultureInfo.InvariantCulture).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        ProcessesListBox.ItemsSource = filtered;
        double totalGb = filtered.Sum(p => p.WorkingSetBytes) / (1024.0 * 1024.0 * 1024.0);
        ProcessSummaryText.Text = LocalizationService.CurrentLanguage == LocalizationService.English
            ? $"{filtered.Count} active processes shown • {totalGb:F1} GB RAM"
            : $"Hiển thị {filtered.Count} tiến trình chạy ngầm • {totalGb:F1} GB RAM";
    }

    private async void OnKillProcessClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not ProcessItemInfo item) return;
        bool isEn = LocalizationService.CurrentLanguage == LocalizationService.English;

        if (item.IsCritical)
        {
            MessageBox.Show(
                isEn ? $"{item.Name} is a protected system process and cannot be ended here."
                     : $"{item.Name} là tiến trình hệ thống được bảo vệ nên không thể tắt tại đây.",
                "LAILauncher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            isEn ? $"End background process '{item.Name}' (PID {item.Pid})?"
                 : $"Bạn có chắc muốn kết thúc tiến trình '{item.Name}' (PID {item.Pid})?",
            "LAILauncher",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var proc = Process.GetProcessById(item.Pid);
            proc.Kill();
            await Task.Delay(200);
            await RefreshRunningProcessesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "LAILauncher", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Tab 4: Windows System Controls

    private async Task RefreshWindowsControlsStateAsync()
    {
        bool isEn = LocalizationService.CurrentLanguage == LocalizationService.English;
        var state = await Task.Run(() =>
        {
            bool dark = WindowsControlService.IsWindowsInDarkMode();
            bool wifi = WindowsControlService.IsWifiEnabled();
            string? ssid = wifi ? WindowsControlService.GetConnectedWifiSsid() : null;
            bool bt = WindowsControlService.IsBluetoothEnabled();
            bool bat = WindowsControlService.IsBatterySaverActive();
            int vol = WindowsControlService.GetMasterVolume();
            bool muted = WindowsControlService.IsMuted();
            int bright = WindowsControlService.GetBrightness();
            return (dark, wifi, ssid, bt, bat, vol, muted, bright);
        });

        WindowsThemeModeStatusText.Text = state.dark
            ? (isEn ? "Windows Mode: Dark" : "Chế độ Windows: Tối (Dark Mode)")
            : (isEn ? "Windows Mode: Light" : "Chế độ Windows: Sáng (Light Mode)");

        WindowsWifiStatusText.Text = state.wifi
            ? (string.IsNullOrWhiteSpace(state.ssid)
                ? (isEn ? "Wi-Fi: On" : "Wi-Fi: Đang bật")
                : $"Wi-Fi: {state.ssid}")
            : (isEn ? "Wi-Fi: Off" : "Wi-Fi: Đang tắt");

        WindowsBluetoothStatusText.Text = state.bt
            ? (isEn ? "Bluetooth: On" : "Bluetooth: Đang bật")
            : (isEn ? "Bluetooth: Off" : "Bluetooth: Đang tắt");

        WindowsBatterySaverStatusText.Text = state.bat
            ? (isEn ? "Energy Saver: Active" : "Tiết kiệm pin: Đang BẬT")
            : (isEn ? "Energy Saver: Off (Balanced)" : "Tiết kiệm pin: Đang TẮT");

        _isSliderSyncing = true;
        try
        {
            WindowsVolumeSlider.Value = Math.Clamp(state.vol, 0, 100);
            WindowsVolumeValueText.Text = state.muted
                ? (isEn ? "Muted" : "Tắt tiếng")
                : $"{state.vol}%";
            WindowsMuteButton.Content = state.muted
                ? (isEn ? "Unmute" : "Bật tiếng")
                : (isEn ? "Mute" : "Tắt tiếng");

            if (state.bright >= 0)
            {
                WindowsBrightnessSlider.IsEnabled = true;
                WindowsBrightnessSlider.Value = Math.Clamp(state.bright, 0, 100);
                WindowsBrightnessValueText.Text = $"{state.bright}%";
            }
            else
            {
                WindowsBrightnessSlider.IsEnabled = false;
                WindowsBrightnessValueText.Text = "N/A";
            }
        }
        finally
        {
            _isSliderSyncing = false;
        }
    }

    private async void OnSetWindowsDarkModeClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetWindowsDarkMode(true);
        await RefreshWindowsControlsStateAsync();
    }

    private async void OnSetWindowsLightModeClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetWindowsDarkMode(false);
        await RefreshWindowsControlsStateAsync();
    }

    private async void OnToggleWindowsDarkModeClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.ToggleWindowsDarkMode();
        await RefreshWindowsControlsStateAsync();
    }

    private async void OnToggleWifiClicked(object sender, RoutedEventArgs e)
    {
        await WindowsControlService.ToggleWifiAsync();
        await RefreshWindowsControlsStateAsync();
    }

    private void OnShowWifiListClicked(object sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow mw)
        {
            mw.OpenWithQuery("sys wifi list");
            Close();
        }
        else
        {
            WindowsControlService.ShowAvailableNetworks();
        }
    }

    private async void OnToggleBluetoothClicked(object sender, RoutedEventArgs e)
    {
        await WindowsControlService.ToggleBluetoothAsync();
        await RefreshWindowsControlsStateAsync();
    }

    private void OnShowBluetoothListClicked(object sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow mw)
        {
            mw.OpenWithQuery("sys bluetooth list");
            Close();
        }
        else
        {
            WindowsControlService.OpenBluetoothSettings();
        }
    }

    private void OnAddBluetoothDeviceClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenAddBluetoothDevice();
    }

    private async void OnToggleBatterySaverClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.ToggleBatterySaver();
        await RefreshWindowsControlsStateAsync();
    }

    private async void OnEnableBatterySaverClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.EnableBatterySaver();
        await RefreshWindowsControlsStateAsync();
    }

    private async void OnDisableBatterySaverClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.DisableBatterySaver();
        await RefreshWindowsControlsStateAsync();
    }

    private void OnWindowsVolumeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || _isSliderSyncing) return;
        int vol = (int)Math.Round(e.NewValue);
        WindowsControlService.SetMasterVolume(vol);
        WindowsVolumeValueText.Text = $"{vol}%";
    }

    private async void OnWindowsToggleMuteClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.ToggleMute();
        await RefreshWindowsControlsStateAsync();
    }

    private void OnOpenVolumeMixerClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenVolumeMixer();
    }

    private void OnWindowsBrightnessSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || _isSliderSyncing) return;
        int b = (int)Math.Round(e.NewValue);
        WindowsControlService.SetBrightness(b);
        WindowsBrightnessValueText.Text = $"{b}%";
    }

    private async void OnSetBrightness25Clicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetBrightness(25);
        await RefreshWindowsControlsStateAsync();
    }

    private async void OnSetBrightness50Clicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetBrightness(50);
        await RefreshWindowsControlsStateAsync();
    }

    private async void OnSetBrightness100Clicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetBrightness(100);
        await RefreshWindowsControlsStateAsync();
    }

    private void OnToggleHotspotClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.ToggleMobileHotspot();
    }

    private void OnSetTaskbarAlignCenterClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetTaskbarAlignment(TaskbarAlignment.Center);
    }

    private void OnSetTaskbarAlignLeftClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetTaskbarAlignment(TaskbarAlignment.Left);
    }

    #endregion

    #region Tab 5: Local AI (Ollama)

    private async Task LoadAISettingsAsync()
    {
        var endpoint = await _settingsRepo.GetAsync("AIOllamaEndpoint");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = _aiProvider?.CurrentEndpoint ?? "http://localhost:11434";
        }
        OllamaEndpointTextBox.Text = endpoint;

        StartOllamaWithAppCheckBox.IsChecked = await GetBoolSettingAsync("AIOllamaStartWithApp", true);
        EnableIntentClassifierCheckBox.IsChecked = await GetBoolSettingAsync("AIEnableIntentClassifier", true);

        await RefreshOllamaStatusAndModelsAsync();
    }

    private async Task RefreshOllamaStatusAndModelsAsync()
    {
        if (_aiProvider == null)
        {
            SetOllamaStatus(false, "AI Provider unavailable");
            return;
        }

        _aiProvider.UpdateEndpoint(OllamaEndpointTextBox.Text.Trim());
        bool healthy = await _aiProvider.CheckHealthAsync();
        bool isEn = LocalizationService.CurrentLanguage == LocalizationService.English;

        SetOllamaStatus(healthy, healthy
            ? (isEn ? $"Connected to {_aiProvider.CurrentEndpoint}" : $"Đã kết nối: {_aiProvider.CurrentEndpoint}")
            : (isEn ? "Ollama server not responding" : "Không thể kết nối máy chủ Ollama"));

        var savedModel = await _settingsRepo.GetAsync("AIOllamaModel");
        var currentModel = !string.IsNullOrWhiteSpace(savedModel) ? savedModel : _aiProvider.DefaultModel;

        if (healthy)
        {
            var models = await _aiProvider.GetAvailableModelsAsync();
            OllamaModelComboBox.ItemsSource = models;
            if (!string.IsNullOrWhiteSpace(currentModel) && models.Contains(currentModel))
            {
                OllamaModelComboBox.SelectedItem = currentModel;
            }
            else if (!string.IsNullOrWhiteSpace(currentModel))
            {
                OllamaModelComboBox.Text = currentModel;
            }
            else if (models.Count > 0)
            {
                OllamaModelComboBox.SelectedIndex = 0;
            }
        }
        else if (!string.IsNullOrWhiteSpace(currentModel))
        {
            OllamaModelComboBox.ItemsSource = new[] { currentModel };
            OllamaModelComboBox.SelectedIndex = 0;
            OllamaModelComboBox.Text = currentModel;
        }
    }

    private void SetOllamaStatus(bool connected, string text)
    {
        OllamaStatusText.Text = text;
        if (OllamaStatusBorder != null)
        {
            OllamaStatusBorder.BorderBrush = connected
                ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))
                : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        }
    }

    private async void OnStartOllamaClicked(object sender, RoutedEventArgs e)
    {
        if (_ollamaProcessService == null) return;
        StartOllamaButton.IsEnabled = false;
        try
        {
            _aiProvider?.UpdateEndpoint(OllamaEndpointTextBox.Text.Trim());
            var result = await _ollamaProcessService.StartAsync();
            SetOllamaStatus(result.Success, result.Message);
            if (result.Success)
            {
                await RefreshOllamaStatusAndModelsAsync();
            }
        }
        finally
        {
            StartOllamaButton.IsEnabled = true;
        }
    }

    private async void OnTestOllamaClicked(object sender, RoutedEventArgs e)
    {
        TestOllamaButton.IsEnabled = false;
        try
        {
            await RefreshOllamaStatusAndModelsAsync();
        }
        finally
        {
            TestOllamaButton.IsEnabled = true;
        }
    }

    private async void OnRefreshModelsClicked(object sender, RoutedEventArgs e)
    {
        RefreshModelsButton.IsEnabled = false;
        try
        {
            await RefreshOllamaStatusAndModelsAsync();
        }
        finally
        {
            RefreshModelsButton.IsEnabled = true;
        }
    }

    #endregion

    #region Tab 6: About, Diagnostics & Footer Actions

    private void LoadAboutInfo()
    {
        if (AboutAppVersionText != null)
        {
            AboutAppVersionText.Text = "v2.4.0 (.NET 8 Windows Desktop)";
        }
        if (AboutRuntimeText != null)
        {
            AboutRuntimeText.Text = $".NET {Environment.Version} ({(Environment.Is64BitProcess ? "x64" : "x86")})";
        }
        if (AboutOsVersionText != null)
        {
            AboutOsVersionText.Text = Environment.OSVersion.VersionString;
        }
        if (AboutDataPathText != null)
        {
            AboutDataPathText.Text = GetAppDataDirectory();
        }
    }

    private static string GetAppDataDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LAILauncher");

    private void OnOpenAppDataClicked(object sender, RoutedEventArgs e)
    {
        var dir = GetAppDataDirectory();
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{dir}\"",
            UseShellExecute = true
        });
    }

    private void OnCopyDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LAILauncher Diagnostics");
        sb.AppendLine($"- Version: 2.4.0");
        sb.AppendLine($"- OS: {Environment.OSVersion}");
        sb.AppendLine($"- Runtime: .NET {Environment.Version} ({(Environment.Is64BitProcess ? "x64" : "x86")})");
        sb.AppendLine($"- Language: {LocalizationService.CurrentLanguage}");
        sb.AppendLine($"- Theme: {ThemeManager.CurrentTheme} (Light={ThemeManager.IsLightMode}, Glass={ThemeManager.IsGlassEffectEnabled}, Opacity={ThemeManager.GlassOpacity:F2})");
        sb.AppendLine($"- Chrome Style: {ThemeManager.CurrentOsStyle} / {ThemeManager.CurrentUiStyle}");
        sb.AppendLine($"- Monitors: {_detectedMonitors.Count}");
        sb.AppendLine($"- Data Path: {GetAppDataDirectory()}");
        Clipboard.SetText(sb.ToString());

        bool isEn = LocalizationService.CurrentLanguage == LocalizationService.English;
        MessageBox.Show(
            isEn ? "Diagnostics copied to clipboard." : "Đã sao chép thông tin chẩn đoán vào Clipboard.",
            "LAILauncher",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void OnResetAllDefaultsClicked(object sender, RoutedEventArgs e)
    {
        bool isEn = LocalizationService.CurrentLanguage == LocalizationService.English;
        var confirm = MessageBox.Show(
            isEn ? "Reset theme, window layout, language, and behavior settings back to factory defaults?"
                 : "Khôi phục giao diện, kích thước cửa sổ, ngôn ngữ và hành vi ứng dụng về mặc định?",
            "LAILauncher",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        await _settingsRepo.SetAsync("AppTheme", ThemeMode.Windows11.ToString());
        await _settingsRepo.SetAsync("AppOsStyle", OsStyle.Windows.ToString());
        await _settingsRepo.SetAsync("AppUiStyle", UiStyle.ModernGlass.ToString());
        await _settingsRepo.SetAsync("AppForcedLightMode", "");
        await _settingsRepo.SetAsync("AppGlassEnabled", "true");
        await _settingsRepo.SetAsync("AppGlassOpacity", "0.85");
        await _settingsRepo.SetAsync("AppGlassTintWithAccent", "false");
        await _settingsRepo.SetAsync("AppAccentFromWindows", "false");
        await _settingsRepo.SetAsync("AppAccentOnBorder", "false");
        await _settingsRepo.SetAsync("AppAccentColor", string.Empty);
        await _settingsRepo.SetAsync("AppGlassTintColor", string.Empty);
        await _settingsRepo.SetAsync("AppLanguage", LocalizationService.Vietnamese);
        await _settingsRepo.SetAsync("AppWindowWidth", "680");
        await _settingsRepo.SetAsync("AppWindowHeight", "460");
        await _settingsRepo.SetAsync("AppWindowCustomPositionEnabled", "false");
        await _settingsRepo.SetAsync("AppShowTrayIcon", "true");
        await _settingsRepo.SetAsync("AppMinimizeToTray", "true");
        await _settingsRepo.SetAsync("AppHideOnDeactivate", "true");
        await _settingsRepo.SetAsync("AppRememberLastQuery", "false");
        await _settingsRepo.SetAsync("AppShowQuickStrip", "false");

        LocalizationService.SetLanguage(LocalizationService.Vietnamese);
        ThemeManager.LoadConfiguration(
            theme: ThemeMode.Windows11,
            accentHex: null,
            useWindowsAccent: false,
            accentOnBorder: false,
            glassEnabled: true,
            glassTintHex: null,
            glassOpacity: 0.85,
            glassTintWithAccent: false,
            osStyle: OsStyle.Windows,
            forcedLightMode: null,
            uiStyle: UiStyle.ModernGlass);

        (Application.Current as App)?.SetTrayIconVisible(true);
        _onWindowSizeUpdated?.Invoke(680, 460);
        _onMoveWindowPreset?.Invoke("TopCenter");
        _onBehaviorSettingsUpdated?.Invoke();
        await LoadSettingsAsync();
    }

    private void OnRestartClicked(object sender, RoutedEventArgs e)
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
        }
        Application.Current.Shutdown();
    }

    private async void OnSaveAndCloseClicked(object sender, RoutedEventArgs e)
    {
        var endpoint = OllamaEndpointTextBox?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            await _settingsRepo.SetAsync("AIOllamaEndpoint", endpoint);
            _aiProvider?.UpdateEndpoint(endpoint);
        }

        var model = !string.IsNullOrWhiteSpace(OllamaModelComboBox?.Text)
            ? OllamaModelComboBox.Text.Trim()
            : OllamaModelComboBox?.SelectedItem as string;
        if (!string.IsNullOrWhiteSpace(model))
        {
            await _settingsRepo.SetAsync("AIOllamaModel", model);
            if (_aiProvider != null)
            {
                _aiProvider.DefaultModel = model;
            }
        }

        await _settingsRepo.SetAsync("AIOllamaStartWithApp", (StartOllamaWithAppCheckBox.IsChecked == true).ToString());
        await _settingsRepo.SetAsync("AIEnableIntentClassifier", (EnableIntentClassifierCheckBox.IsChecked == true).ToString());

        Close();
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    #endregion
}
