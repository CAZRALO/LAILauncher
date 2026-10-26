using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.App.Services;
using Launcher.Database.Repositories;
using Launcher.AI;
using Launcher.Core.Utilities;

namespace Launcher.App;

public partial class SettingsWindow : Window
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly GlobalHotkey _hotkey;
    private readonly Action<string> _onHotkeyUpdated;
    private readonly IAIProvider? _aiProvider;
    private readonly OllamaProcessService? _ollamaProcessService;
    private readonly Action<MonitorTargetMode, string?, WindowVerticalPosition>? _onMonitorSettingsUpdated;
    private readonly Action<string>? _onTestMonitor;
    private bool _isInitializing = true;

    private ModifierKeys _customModifiers = ModifierKeys.None;
    private Key _customKey = Key.None;

    public SettingsWindow(
        ISettingsRepository settingsRepo,
        GlobalHotkey hotkey,
        Action<string> onHotkeyUpdated,
        IAIProvider? aiProvider = null,
        OllamaProcessService? ollamaProcessService = null,
        Action<MonitorTargetMode, string?, WindowVerticalPosition>? onMonitorSettingsUpdated = null,
        Action<string>? onTestMonitor = null)
    {
        InitializeComponent();
        _settingsRepo = settingsRepo;
        _hotkey = hotkey;
        _onHotkeyUpdated = onHotkeyUpdated;
        _aiProvider = aiProvider;
        _ollamaProcessService = ollamaProcessService;
        _onMonitorSettingsUpdated = onMonitorSettingsUpdated;
        _onTestMonitor = onTestMonitor;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;

        // 0. Enable Windows Hardware Blur (Acrylic / BlurBehind)
        ThemeManager.EnableWindowBlur(this);

        // 0.5 Load OS Style & Dual Mode
        if (ThemeManager.CurrentOsStyle == OsStyle.Linux)
        {
            OsStyleLinuxRadio.IsChecked = true;
        }
        else
        {
            OsStyleWindowsRadio.IsChecked = true;
        }

        if (ThemeManager.IsLightMode)
        {
            ModeForceLightRadio.IsChecked = true;
        }
        else
        {
            ModeForceDarkRadio.IsChecked = true;
        }

        // 1. Load Theme
        var currentTheme = ThemeManager.CurrentTheme;
        switch (currentTheme)
        {
            case ThemeMode.System:
                ThemeSystemRadio.IsChecked = true;
                break;
            case ThemeMode.Ubuntu:
                ThemeUbuntuRadio.IsChecked = true;
                break;
            case ThemeMode.Arch:
                ThemeArchRadio.IsChecked = true;
                break;
            case ThemeMode.Aurora:
                ThemeAuroraRadio.IsChecked = true;
                break;
            case ThemeMode.Cyberpunk:
                ThemeCyberpunkRadio.IsChecked = true;
                break;
            case ThemeMode.Nord:
                ThemeNordRadio.IsChecked = true;
                break;
            case ThemeMode.Dracula:
                ThemeDraculaRadio.IsChecked = true;
                break;
            case ThemeMode.Emerald:
                ThemeEmeraldRadio.IsChecked = true;
                break;
            case ThemeMode.Sunset:
                ThemeSunsetRadio.IsChecked = true;
                break;
            case ThemeMode.Linux:
                ThemeArchRadio.IsChecked = true;
                break;
            case ThemeMode.Dark:
            case ThemeMode.Light:
            case ThemeMode.Glass:
            case ThemeMode.TokyoNight:
            default:
                ThemeWinFluentRadio.IsChecked = true;
                break;
        }

        // 1.5 Load Glass Appearance
        bool isGlass = ThemeManager.IsGlassEffectEnabled || currentTheme == ThemeMode.Glass;
        EnableGlassCheckBox.IsChecked = isGlass;
        GlassOptionsPanel.Visibility = isGlass ? Visibility.Visible : Visibility.Collapsed;

        GlassTintWithAccentCheckBox.IsChecked = ThemeManager.GlassTintWithAccent;
        ManualGlassTintPanel.IsEnabled = !ThemeManager.GlassTintWithAccent;

        var activeGlassTint = ThemeManager.ActiveGlassTintHex;
        CustomGlassTintTextBox.Text = activeGlassTint;
        UpdateGlassTintSwatch(activeGlassTint);

        var glassOpacityPct = Math.Round(ThemeManager.GlassOpacity * 100);
        GlassOpacitySlider.Value = glassOpacityPct;
        GlassOpacityText.Text = $"{glassOpacityPct}%";

        // 1.8 Load Accent Color & Border Accent
        UseWindowsAccentCheckBox.IsChecked = ThemeManager.UseWindowsAccentColor;
        AccentPalettePanel.IsEnabled = !ThemeManager.UseWindowsAccentColor;
        WindowsAccentHintText.Text = $"Windows: {ThemeManager.GetWindowsAccentColorHex()}";

        AccentOnBorderCheckBox.IsChecked = ThemeManager.ApplyAccentToBorder;

        var activeAccent = ThemeManager.ActiveAccentHex;
        CustomAccentTextBox.Text = activeAccent;
        UpdateAccentSwatch(activeAccent);
        UpdateAccentChipsHighlight(activeAccent);

        // 2. Load Hotkey
        var hotkeyStr = GlobalHotkey.FormatHotkey(_hotkey.Modifiers, _hotkey.Key);
        UpdateHotkeySelectionUI(hotkeyStr);

        // 3. Load Auto-start
        AutoStartCheckBox.IsChecked = StartupService.IsStartupEnabled();

        // 4. Load AI Settings
        var endpoint = await _settingsRepo.GetAsync("AIOllamaEndpoint");
        OllamaEndpointTextBox.Text = !string.IsNullOrWhiteSpace(endpoint) ? endpoint : (_aiProvider?.CurrentEndpoint ?? "http://localhost:11434");

        var model = await _settingsRepo.GetAsync("AIOllamaModel");
        var activeModel = !string.IsNullOrWhiteSpace(model) ? model : (_aiProvider?.DefaultModel ?? "llama3.2:1b");

        var enableRouter = await _settingsRepo.GetAsync("AIEnableIntentClassifier");
        EnableIntentClassifierCheckBox.IsChecked = string.IsNullOrEmpty(enableRouter) || bool.TryParse(enableRouter, out var b) && b;

        var startOllamaWithApp = await _settingsRepo.GetAsync("AIOllamaStartWithApp");
        StartOllamaWithAppCheckBox.IsChecked = bool.TryParse(startOllamaWithApp, out var startOllama) && startOllama;

        await RefreshModelsListAsync(activeModel);

        // 5. Load Windows System Controls state
        try
        {
            bool isWindowsDark = WindowsControlService.IsWindowsInDarkMode();
            WindowsThemeModeStatusText.Text = isWindowsDark ? "Hiện tại: Đang ở chế độ Tối (Dark Mode)" : "Hiện tại: Đang ở chế độ Sáng (Light Mode)";

            int vol = WindowsControlService.GetMasterVolume();
            WindowsVolumeSlider.Value = vol;
            WindowsVolumeValueText.Text = $"{vol}%";

            int brightness = WindowsControlService.GetBrightness();
            WindowsBrightnessSlider.Value = brightness;
            WindowsBrightnessValueText.Text = $"{brightness}%";

            bool isMuted = WindowsControlService.IsMuted();
            WindowsMuteButton.Content = isMuted ? "🔊 Unmute" : "🔇 Mute";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading Windows state: {ex.Message}");
        }

        // 6. Load Monitor / Display Settings
        await LoadMonitorSettingsAsync();

        _isInitializing = false;
    }

    private void UpdateHotkeySelectionUI(string hotkeyStr)
    {
        var normalized = hotkeyStr.Replace(" ", "").ToLowerInvariant();
        if (normalized == "alt+space")
        {
            HotkeyPresetCombo.SelectedIndex = 0;
            CustomHotkeyPanel.Visibility = Visibility.Collapsed;
        }
        else if (normalized == "ctrl+space")
        {
            HotkeyPresetCombo.SelectedIndex = 1;
            CustomHotkeyPanel.Visibility = Visibility.Collapsed;
        }
        else if (normalized == "win+space")
        {
            HotkeyPresetCombo.SelectedIndex = 2;
            CustomHotkeyPanel.Visibility = Visibility.Collapsed;
        }
        else if (normalized == "alt+k")
        {
            HotkeyPresetCombo.SelectedIndex = 3;
            CustomHotkeyPanel.Visibility = Visibility.Collapsed;
        }
        else if (normalized == "ctrl+shift+space")
        {
            HotkeyPresetCombo.SelectedIndex = 4;
            CustomHotkeyPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            HotkeyPresetCombo.SelectedIndex = 5; // Custom
            CustomHotkeyPanel.Visibility = Visibility.Visible;
            CustomHotkeyBox.Text = hotkeyStr;
        }

        HotkeyStatusText.Text = _hotkey.IsRegistered
            ? (_hotkey.RegistrationType == HotkeyRegistrationType.LowLevelHook
                ? $"Phím tắt đang hoạt động: {hotkeyStr} (Chế độ tương thích - ghi đè app khác)"
                : $"Phím tắt đang hoạt động: {hotkeyStr}")
            : $"Không thể đăng ký phím tắt: {hotkeyStr}";
    }

    private void UpdateAccentSwatch(string hex)
    {
        try
        {
            var cleanHex = hex.Trim();
            if (!cleanHex.StartsWith('#')) cleanHex = "#" + cleanHex;
            var color = ThemeManager.ColorFromHex(cleanHex);
            AccentColorPreviewSwatch.Background = new SolidColorBrush(color);
        }
        catch
        {
            // Invalid hex, keep existing
        }
    }

    private void UpdateGlassTintSwatch(string hex)
    {
        try
        {
            var cleanHex = hex.Trim();
            if (!cleanHex.StartsWith('#')) cleanHex = "#" + cleanHex;
            var color = ThemeManager.ColorFromHex(cleanHex);
            GlassTintPreviewSwatch.Background = new SolidColorBrush(color);
        }
        catch
        {
            // Invalid hex, keep existing
        }
    }

    private async void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        if (sender is not RadioButton { IsChecked: true } rb) return;

        ThemeMode selectedTheme = rb.Name switch
        {
            "ThemeWinFluentRadio" => ThemeMode.Dark,
            "ThemeUbuntuRadio" => ThemeMode.Ubuntu,
            "ThemeArchRadio" => ThemeMode.Arch,
            "ThemeAuroraRadio" => ThemeMode.Aurora,
            "ThemeCyberpunkRadio" => ThemeMode.Cyberpunk,
            "ThemeNordRadio" => ThemeMode.Nord,
            "ThemeDraculaRadio" => ThemeMode.Dracula,
            "ThemeEmeraldRadio" => ThemeMode.Emerald,
            "ThemeSunsetRadio" => ThemeMode.Sunset,
            "ThemeSystemRadio" => ThemeMode.System,
            _ => ThemeMode.Dark
        };

        ThemeManager.ApplyTheme(selectedTheme);
        await _settingsRepo.SetAsync("AppTheme", selectedTheme.ToString());

        // When switching preset theme, clear any custom manual accent/glass tint overrides in DB
        await _settingsRepo.SetAsync("AppAccentColor", string.Empty);
        await _settingsRepo.SetAsync("AppGlassTintColor", string.Empty);

        bool isSys = (selectedTheme == ThemeMode.System);
        UseWindowsAccentCheckBox.IsChecked = isSys;
        AccentPalettePanel.IsEnabled = !isSys;
        await _settingsRepo.SetAsync("AppAccentFromWindows", isSys.ToString());

        // Update accent display
        var activeAccent = ThemeManager.ActiveAccentHex;
        CustomAccentTextBox.Text = activeAccent;
        UpdateAccentSwatch(activeAccent);
        UpdateAccentChipsHighlight(activeAccent);

        // Update glass display
        var activeTint = ThemeManager.ActiveGlassTintHex;
        CustomGlassTintTextBox.Text = activeTint;
        UpdateGlassTintSwatch(activeTint);
    }

    private async void OnOsStyleSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var newOs = OsStyleLinuxRadio.IsChecked == true ? OsStyle.Linux : OsStyle.Windows;
        ThemeManager.SetOsStyle(newOs);
        await _settingsRepo.SetAsync("AppOsStyle", newOs.ToString());
    }

    private async void OnDualModeSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool isLight = ModeForceLightRadio.IsChecked == true;
        ThemeManager.SetLightMode(isLight);
        await _settingsRepo.SetAsync("AppForcedLightMode", isLight.ToString());
    }

    // --- Glass Appearance Event Handlers ---

    private async void OnEnableGlassChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool isEnabled = EnableGlassCheckBox.IsChecked == true;
        GlassOptionsPanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;

        ThemeManager.SetGlassEffect(isEnabled);
        await _settingsRepo.SetAsync("AppGlassEnabled", isEnabled.ToString());
    }

    private async void OnGlassTintWithAccentChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool tintWithAccent = GlassTintWithAccentCheckBox.IsChecked == true;
        ManualGlassTintPanel.IsEnabled = !tintWithAccent;

        ThemeManager.SetGlassTintWithAccent(tintWithAccent);
        await _settingsRepo.SetAsync("AppGlassTintWithAccent", tintWithAccent.ToString());

        var activeTint = ThemeManager.ActiveGlassTintHex;
        CustomGlassTintTextBox.Text = activeTint;
        UpdateGlassTintSwatch(activeTint);
    }

    private async void OnPresetGlassTintClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            CustomGlassTintTextBox.Text = hex;
            UpdateGlassTintSwatch(hex);
            ThemeManager.SetGlassTint(hex);
            await _settingsRepo.SetAsync("AppGlassTintColor", hex);
        }
    }

    private void OnCustomGlassTintTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        UpdateGlassTintSwatch(CustomGlassTintTextBox.Text);
    }

    private async void OnApplyGlassTintClicked(object sender, RoutedEventArgs e)
    {
        var hex = CustomGlassTintTextBox.Text;
        if (ThemeManager.SetGlassTint(hex))
        {
            UpdateGlassTintSwatch(hex);
            await _settingsRepo.SetAsync("AppGlassTintColor", hex);
        }
    }

    private async void OnResetGlassTintClicked(object sender, RoutedEventArgs e)
    {
        ThemeManager.SetGlassTint(null);
        var defaultTint = ThemeManager.ActiveGlassTintHex;
        CustomGlassTintTextBox.Text = defaultTint;
        UpdateGlassTintSwatch(defaultTint);
        await _settingsRepo.SetAsync("AppGlassTintColor", string.Empty);
    }

    private async void OnGlassOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        int pct = (int)Math.Round(GlassOpacitySlider.Value);
        GlassOpacityText.Text = $"{pct}%";
        double opacity = pct / 100.0;

        ThemeManager.SetGlassOpacity(opacity);
        await _settingsRepo.SetAsync("AppGlassOpacity", opacity.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // --- Accent Color Event Handlers ---

    private async void OnUseWindowsAccentChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool useWin = UseWindowsAccentCheckBox.IsChecked == true;
        AccentPalettePanel.IsEnabled = !useWin;

        ThemeManager.SetUseWindowsAccentColor(useWin);
        var activeHex = ThemeManager.ActiveAccentHex;
        CustomAccentTextBox.Text = activeHex;
        UpdateAccentSwatch(activeHex);

        await _settingsRepo.SetAsync("AppAccentFromWindows", useWin.ToString());
    }

    private async void OnAccentOnBorderChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool onBorder = AccentOnBorderCheckBox.IsChecked == true;

        ThemeManager.SetApplyAccentToBorder(onBorder);
        await _settingsRepo.SetAsync("AppAccentOnBorder", onBorder.ToString());
    }

    private void OnCustomAccentTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        UpdateAccentSwatch(CustomAccentTextBox.Text);
    }

    private void OnPresetAccentClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            ApplyAndSaveAccent(hex);
        }
    }

    private void OnApplyAccentClicked(object sender, RoutedEventArgs e)
    {
        ApplyAndSaveAccent(CustomAccentTextBox.Text);
    }

    private void OnResetAccentClicked(object sender, RoutedEventArgs e)
    {
        ApplyAndSaveAccent(null);
    }

    private async void ApplyAndSaveAccent(string? hex)
    {
        UseWindowsAccentCheckBox.IsChecked = false;
        AccentPalettePanel.IsEnabled = true;
        ThemeManager.SetUseWindowsAccentColor(false);
        await _settingsRepo.SetAsync("AppAccentFromWindows", "False");

        ThemeManager.ApplyCustomAccentColor(hex);
        var activeHex = ThemeManager.ActiveAccentHex;
        CustomAccentTextBox.Text = activeHex;
        UpdateAccentSwatch(activeHex);
        UpdateAccentChipsHighlight(activeHex);

        await _settingsRepo.SetAsync("AppAccentColor", hex ?? string.Empty);
    }

    private void UpdateAccentChipsHighlight(string? hex)
    {
        if (AccentChipsWrapPanel == null) return;
        var norm = hex?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!norm.StartsWith("#") && norm.Length > 0) norm = "#" + norm;

        foreach (var child in AccentChipsWrapPanel.Children)
        {
            if (child is Button btn)
            {
                var tag = (btn.Tag as string)?.Trim().ToUpperInvariant() ?? string.Empty;
                bool isMatch = !string.IsNullOrEmpty(norm) && tag == norm;
                btn.BorderBrush = isMatch ? new SolidColorBrush(Colors.White) : (Brush)FindResource("CardBorderBrush");
                btn.BorderThickness = isMatch ? new Thickness(2.5) : new Thickness(1.5);
            }
        }
    }

    private async void OnHotkeyPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var selectedIndex = HotkeyPresetCombo.SelectedIndex;
        if (selectedIndex == 5)
        {
            CustomHotkeyPanel.Visibility = Visibility.Visible;
            CustomHotkeyBox.Focus();
            return;
        }

        CustomHotkeyPanel.Visibility = Visibility.Collapsed;
        (ModifierKeys mods, Key k) = selectedIndex switch
        {
            0 => (ModifierKeys.Alt, Key.Space),
            1 => (ModifierKeys.Control, Key.Space),
            2 => (ModifierKeys.Windows, Key.Space),
            3 => (ModifierKeys.Alt, Key.K),
            4 => (ModifierKeys.Control | ModifierKeys.Shift, Key.Space),
            _ => (ModifierKeys.Alt, Key.Space)
        };

        ApplyAndSaveHotkey(mods, k);
    }

    private void OnCustomHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            CustomHotkeyBox.Text = "Cần ít nhất một phím bổ trợ (Alt, Ctrl, Shift, Win)";
            return;
        }

        _customModifiers = modifiers;
        _customKey = key;

        CustomHotkeyBox.Text = GlobalHotkey.FormatHotkey(modifiers, key);
    }

    private void OnApplyCustomHotkeyClicked(object sender, RoutedEventArgs e)
    {
        if (_customModifiers != ModifierKeys.None && _customKey != Key.None)
        {
            ApplyAndSaveHotkey(_customModifiers, _customKey);
        }
        else if (GlobalHotkey.TryParseHotkey(CustomHotkeyBox.Text, out var mods, out var key))
        {
            ApplyAndSaveHotkey(mods, key);
        }
    }

    private async void ApplyAndSaveHotkey(ModifierKeys modifiers, Key key)
    {
        var success = _hotkey.UpdateHotkey(modifiers, key);
        var formatted = GlobalHotkey.FormatHotkey(modifiers, key);

        HotkeyStatusText.Text = success
            ? (_hotkey.RegistrationType == HotkeyRegistrationType.LowLevelHook
                ? $"Phím tắt đang hoạt động: {formatted} (Chế độ tương thích - ghi đè app khác)"
                : $"Phím tắt đang hoạt động: {formatted}")
            : $"Không thể đăng ký phím tắt: {formatted}";

        await _settingsRepo.SetAsync("AppHotkey", formatted);
        _onHotkeyUpdated?.Invoke(formatted);
    }

    private async void OnAutoStartToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var enabled = AutoStartCheckBox.IsChecked == true;
        StartupService.SetStartup(enabled);
        await _settingsRepo.SetAsync("AutoStartWithWindows", enabled.ToString());
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private async void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        await SaveAiSettingsAsync();
        Close();
    }

    private async void OnSaveAndCloseClicked(object sender, RoutedEventArgs e)
    {
        await SaveAiSettingsAsync();
        Close();
    }

    private async void OnRestartClicked(object sender, RoutedEventArgs e)
    {
        await SaveAiSettingsAsync();

        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new InvalidOperationException("Không xác định được đường dẫn ứng dụng.");
            }

            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true
            };

            if (Path.GetFileNameWithoutExtension(executablePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrWhiteSpace(entryAssemblyPath))
                {
                    throw new InvalidOperationException("Không xác định được assembly ứng dụng.");
                }

                startInfo.ArgumentList.Add(entryAssemblyPath);
            }

            Process.Start(startInfo);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể khởi động lại ứng dụng: {ex.Message}", "LAILauncher", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveAiSettingsAsync()
    {
        try
        {
            var endpoint = OllamaEndpointTextBox.Text.Trim();
            var model = OllamaModelComboBox.Text.Trim();
            var enableRouter = EnableIntentClassifierCheckBox.IsChecked == true;
            var startOllamaWithApp = StartOllamaWithAppCheckBox.IsChecked == true;

            await _settingsRepo.SetAsync("AIOllamaEndpoint", endpoint);
            if (!string.IsNullOrEmpty(model))
            {
                await _settingsRepo.SetAsync("AIOllamaModel", model);
            }
            await _settingsRepo.SetAsync("AIEnableIntentClassifier", enableRouter.ToString());
            await _settingsRepo.SetAsync("AIOllamaStartWithApp", startOllamaWithApp.ToString());

            if (_aiProvider != null)
            {
                _aiProvider.UpdateEndpoint(endpoint);
                if (!string.IsNullOrEmpty(model))
                {
                    _aiProvider.DefaultModel = model;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Error saving AI settings: {ex.Message}");
        }
    }

    private async void OnTestOllamaClicked(object sender, RoutedEventArgs e)
    {
        TestOllamaButton.IsEnabled = false;
        OllamaStatusText.Text = "Đang kiểm tra...";
        OllamaStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        OllamaStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x64, 0x74, 0x8B));
        OllamaStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x64, 0x74, 0x8B));

        var testEndpoint = OllamaEndpointTextBox.Text.Trim();
        if (_aiProvider != null)
        {
            _aiProvider.UpdateEndpoint(testEndpoint);
            var isOnline = await _aiProvider.CheckHealthAsync();
            if (isOnline)
            {
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x10, 0xB9, 0x81));
                OllamaStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x10, 0xB9, 0x81));
                OllamaStatusText.Text = "Đã kết nối";
                OllamaStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                await RefreshModelsListAsync(OllamaModelComboBox.Text);
            }
            else
            {
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xEF, 0x44, 0x44));
                OllamaStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xEF, 0x44, 0x44));
                OllamaStatusText.Text = "Ngoại tuyến";
                OllamaStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            }
        }

        TestOllamaButton.IsEnabled = true;
    }

    private async void OnStartOllamaClicked(object sender, RoutedEventArgs e)
    {
        StartOllamaButton.IsEnabled = false;
        StartOllamaButton.Content = "Đang bật...";
        try
        {
            await SaveAiSettingsAsync();
            if (_ollamaProcessService == null)
            {
                SetOllamaStatus(false, "Không thể quản lý tiến trình Ollama.");
                return;
            }

            var result = await _ollamaProcessService.StartAsync();
            SetOllamaStatus(result.Success, result.Message);
            if (result.Success)
            {
                await RefreshModelsListAsync(OllamaModelComboBox.Text);
            }
        }
        finally
        {
            StartOllamaButton.IsEnabled = true;
            StartOllamaButton.Content = "▶ Khởi chạy";
        }
    }

    private void SetOllamaStatus(bool isOnline, string message)
    {
        var color = isOnline ? Color.FromRgb(0x10, 0xB9, 0x81) : Color.FromRgb(0xEF, 0x44, 0x44);
        OllamaStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x22, color.R, color.G, color.B));
        OllamaStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, color.R, color.G, color.B));
        OllamaStatusText.Text = message;
        OllamaStatusText.Foreground = new SolidColorBrush(color);
    }

    private async void OnRefreshModelsClicked(object sender, RoutedEventArgs e)
    {
        RefreshModelsButton.IsEnabled = false;
        await RefreshModelsListAsync(OllamaModelComboBox.Text);
        RefreshModelsButton.IsEnabled = true;
    }

    private async Task RefreshModelsListAsync(string? selectedModel)
    {
        if (_aiProvider == null) return;

        var currentSelected = !string.IsNullOrWhiteSpace(selectedModel) ? selectedModel : OllamaModelComboBox.Text;
        var models = await _aiProvider.GetAvailableModelsAsync();

        OllamaModelComboBox.ItemsSource = models;
        if (!string.IsNullOrEmpty(currentSelected) && models.Contains(currentSelected))
        {
            OllamaModelComboBox.SelectedItem = currentSelected;
        }
        else if (models.Count > 0)
        {
            OllamaModelComboBox.SelectedIndex = 0;
        }
    }

    #region Windows System Control Event Handlers

    private void OnToggleWindowsDarkModeClicked(object sender, RoutedEventArgs e)
    {
        bool isDark = WindowsControlService.ToggleWindowsDarkMode();
        WindowsThemeModeStatusText.Text = isDark ? "Hiện tại: Đang ở chế độ Tối (Dark Mode)" : "Hiện tại: Đang ở chế độ Sáng (Light Mode)";
    }

    private void OnSetWindowsDarkModeClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetWindowsDarkMode(true);
        WindowsThemeModeStatusText.Text = "Hiện tại: Đang ở chế độ Tối (Dark Mode)";
    }

    private void OnSetWindowsLightModeClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetWindowsDarkMode(false);
        WindowsThemeModeStatusText.Text = "Hiện tại: Đang ở chế độ Sáng (Light Mode)";
    }

    private void OnWindowsAccentChipClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            WindowsControlService.SetWindowsAccentColor(hex);
        }
    }

    private void OnOpenWindowsColorSettingsClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenPersonalizationColors();
    }

    private void OnWindowsVolumeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        int val = (int)Math.Round(WindowsVolumeSlider.Value);
        WindowsVolumeValueText.Text = $"{val}%";
        WindowsControlService.SetMasterVolume(val);
    }

    private void OnWindowsToggleMuteClicked(object sender, RoutedEventArgs e)
    {
        bool isMuted = WindowsControlService.ToggleMute();
        WindowsMuteButton.Content = isMuted ? "🔊 Unmute" : "🔇 Mute";
    }

    private void OnWindowsVolumeDownClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.VolumeDown(3);
        int vol = WindowsControlService.GetMasterVolume();
        WindowsVolumeSlider.Value = vol;
        WindowsVolumeValueText.Text = $"{vol}%";
    }

    private void OnWindowsVolumeUpClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.VolumeUp(3);
        int vol = WindowsControlService.GetMasterVolume();
        WindowsVolumeSlider.Value = vol;
        WindowsVolumeValueText.Text = $"{vol}%";
    }

    private void OnOpenVolumeMixerClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenVolumeMixer();
    }

    private void OnWindowsBrightnessSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        int val = (int)Math.Round(WindowsBrightnessSlider.Value);
        WindowsBrightnessValueText.Text = $"{val}%";
        WindowsControlService.SetBrightness(val);
    }

    private void OnWindowsBrightnessDownClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.BrightnessDown(10);
        int b = WindowsControlService.GetBrightness();
        WindowsBrightnessSlider.Value = b;
        WindowsBrightnessValueText.Text = $"{b}%";
    }

    private void OnWindowsBrightnessUpClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.BrightnessUp(10);
        int b = WindowsControlService.GetBrightness();
        WindowsBrightnessSlider.Value = b;
        WindowsBrightnessValueText.Text = $"{b}%";
    }

    private void OnSetBrightness25Clicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetBrightness(25);
        WindowsBrightnessSlider.Value = 25;
        WindowsBrightnessValueText.Text = "25%";
    }

    private void OnSetBrightness50Clicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetBrightness(50);
        WindowsBrightnessSlider.Value = 50;
        WindowsBrightnessValueText.Text = "50%";
    }

    private void OnSetBrightness100Clicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetBrightness(100);
        WindowsBrightnessSlider.Value = 100;
        WindowsBrightnessValueText.Text = "100%";
    }

    private void OnOpenDisplaySettingsClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenDisplaySettings();
    }

    private void OnShowWifiListClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.ShowAvailableNetworks();
    }

    private void OnTurnOnWifiClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetWifiEnabled(true);
    }

    private void OnTurnOffWifiClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetWifiEnabled(false);
    }

    private void OnOpenBluetoothClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenBluetoothSettings();
    }

    private void OnAddBluetoothDeviceClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenAddBluetoothDevice();
    }

    private void OnToggleBluetoothClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.ToggleBluetooth();
    }

    private void OnOpenHotspotSettingsClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenMobileHotspotSettings();
    }

    private void OnToggleHotspotClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.ToggleMobileHotspot();
    }

    private void OnEnableBatterySaverClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.EnableBatterySaver();
    }

    private void OnDisableBatterySaverClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.DisableBatterySaver();
    }

    private void OnOpenBatterySettingsClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenBatterySaverSettings();
    }

    private void OnSetTaskbarBottomClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetTaskbarPosition(TaskbarPosition.Bottom);
    }

    private void OnSetTaskbarTopClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetTaskbarPosition(TaskbarPosition.Top);
    }

    private void OnSetTaskbarLeftClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetTaskbarPosition(TaskbarPosition.Left);
    }

    private void OnSetTaskbarRightClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetTaskbarPosition(TaskbarPosition.Right);
    }

    private void OnSetTaskbarAlignCenterClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetTaskbarAlignment(TaskbarAlignment.Center);
    }

    private void OnSetTaskbarAlignLeftClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.SetTaskbarAlignment(TaskbarAlignment.Left);
    }

    private void OnOpenTaskbarSettingsClicked(object sender, RoutedEventArgs e)
    {
        WindowsControlService.OpenTaskbarSettings();
    }

    #endregion

    #region Monitor & Display Settings

    private async Task LoadMonitorSettingsAsync()
    {
        try
        {
            var savedModeStr = await _settingsRepo.GetAsync("AppMonitorTargetMode");
            var targetMode = MonitorTargetMode.Cursor;
            if (!string.IsNullOrEmpty(savedModeStr) && Enum.TryParse<MonitorTargetMode>(savedModeStr, true, out var parsedMode))
            {
                targetMode = parsedMode;
            }

            var savedDevice = await _settingsRepo.GetAsync("AppMonitorSpecificDevice");

            var savedPosStr = await _settingsRepo.GetAsync("AppWindowPositionMode");
            var posMode = WindowVerticalPosition.TopCenter;
            if (!string.IsNullOrEmpty(savedPosStr) && Enum.TryParse<WindowVerticalPosition>(savedPosStr, true, out var parsedPos))
            {
                posMode = parsedPos;
            }

            switch (targetMode)
            {
                case MonitorTargetMode.Cursor:
                    MonitorModeCursorRadio.IsChecked = true;
                    SpecificMonitorPanel.Visibility = Visibility.Collapsed;
                    break;
                case MonitorTargetMode.Primary:
                    MonitorModePrimaryRadio.IsChecked = true;
                    SpecificMonitorPanel.Visibility = Visibility.Collapsed;
                    break;
                case MonitorTargetMode.ActiveWindow:
                    MonitorModeActiveWinRadio.IsChecked = true;
                    SpecificMonitorPanel.Visibility = Visibility.Collapsed;
                    break;
                case MonitorTargetMode.Specific:
                    MonitorModeSpecificRadio.IsChecked = true;
                    SpecificMonitorPanel.Visibility = Visibility.Visible;
                    break;
            }

            if (posMode == WindowVerticalPosition.Center)
            {
                PositionCenterRadio.IsChecked = true;
            }
            else
            {
                PositionTopCenterRadio.IsChecked = true;
            }

            var savedCustomPosEnabled = await _settingsRepo.GetAsync("AppWindowCustomPositionEnabled");
            bool hasCustomPos = bool.TryParse(savedCustomPosEnabled, out var cpe) && cpe;
            var savedX = await _settingsRepo.GetAsync("AppWindowCustomPositionX");
            var savedY = await _settingsRepo.GetAsync("AppWindowCustomPositionY");
            UpdateSavedPositionStatusUI(hasCustomPos, savedX, savedY);

            RefreshMonitorsList(savedDevice);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] Error loading monitor settings: {ex.Message}");
        }
    }

    private void OnRefreshMonitorsClicked(object sender, RoutedEventArgs e)
    {
        string? currentSelectedDevice = (SpecificMonitorCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        RefreshMonitorsList(currentSelectedDevice);
    }

    private async void OnMonitorModeSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var mode = MonitorTargetMode.Cursor;
        if (MonitorModePrimaryRadio.IsChecked == true) mode = MonitorTargetMode.Primary;
        else if (MonitorModeActiveWinRadio.IsChecked == true) mode = MonitorTargetMode.ActiveWindow;
        else if (MonitorModeSpecificRadio.IsChecked == true) mode = MonitorTargetMode.Specific;

        SpecificMonitorPanel.Visibility = (mode == MonitorTargetMode.Specific) ? Visibility.Visible : Visibility.Collapsed;

        string? specificDevice = (SpecificMonitorCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        var vPos = PositionCenterRadio.IsChecked == true ? WindowVerticalPosition.Center : WindowVerticalPosition.TopCenter;

        await _settingsRepo.SetAsync("AppMonitorTargetMode", mode.ToString());
        _onMonitorSettingsUpdated?.Invoke(mode, specificDevice, vPos);
    }

    private async void OnSpecificMonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (SpecificMonitorCombo.SelectedItem is ComboBoxItem item && item.Tag is string devTag)
        {
            await _settingsRepo.SetAsync("AppMonitorSpecificDevice", devTag);

            var mode = MonitorModeSpecificRadio.IsChecked == true ? MonitorTargetMode.Specific : MonitorTargetMode.Cursor;
            var vPos = PositionCenterRadio.IsChecked == true ? WindowVerticalPosition.Center : WindowVerticalPosition.TopCenter;
            _onMonitorSettingsUpdated?.Invoke(mode, devTag, vPos);
        }
    }

    private async void OnWindowPositionChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var vPos = PositionCenterRadio.IsChecked == true ? WindowVerticalPosition.Center : WindowVerticalPosition.TopCenter;
        await _settingsRepo.SetAsync("AppWindowPositionMode", vPos.ToString());

        var mode = MonitorTargetMode.Cursor;
        if (MonitorModePrimaryRadio.IsChecked == true) mode = MonitorTargetMode.Primary;
        else if (MonitorModeActiveWinRadio.IsChecked == true) mode = MonitorTargetMode.ActiveWindow;
        else if (MonitorModeSpecificRadio.IsChecked == true) mode = MonitorTargetMode.Specific;

        string? specificDevice = (SpecificMonitorCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        _onMonitorSettingsUpdated?.Invoke(mode, specificDevice, vPos);
    }

    private void UpdateSavedPositionStatusUI(bool hasCustomPos, string? posX, string? posY)
    {
        if (SavedPositionStatusText == null) return;
        if (hasCustomPos && !string.IsNullOrEmpty(posX) && !string.IsNullOrEmpty(posY))
        {
            if (double.TryParse(posX, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var px) &&
                double.TryParse(posY, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var py))
            {
                SavedPositionStatusText.Text = $"Vị trí cửa sổ: Tùy chỉnh (X: {(int)px}, Y: {(int)py})";
                if (ResetPositionSettingsBtn != null) ResetPositionSettingsBtn.IsEnabled = true;
                return;
            }
        }
        SavedPositionStatusText.Text = "Vị trí cửa sổ: Mặc định (Tự động canh giữa)";
    }

    private async void OnResetPositionSettingsClicked(object sender, RoutedEventArgs e)
    {
        await _settingsRepo.SetAsync("AppWindowCustomPositionEnabled", "false");
        if (Owner is MainWindow mw)
        {
            await mw.ResetPositionToCenterAsync();
        }
        UpdateSavedPositionStatusUI(false, null, null);
    }

    private void RefreshMonitorsList(string? selectedDevice = null)
    {
        try
        {
            var monitors = MonitorService.GetMonitors();

            if (MonitorsCountBadge != null)
            {
                MonitorsCountBadge.Text = $"{monitors.Count} màn hình";
            }

            // Populate Specific Monitor Combo
            if (SpecificMonitorCombo != null)
            {
                SpecificMonitorCombo.Items.Clear();
                int selectedIdx = 0;
                for (int i = 0; i < monitors.Count; i++)
                {
                    var m = monitors[i];
                    var item = new ComboBoxItem
                    {
                        Content = $"{m.DisplayName} — {m.ResolutionText} ({m.DeviceName})",
                        Tag = m.DeviceName
                    };
                    SpecificMonitorCombo.Items.Add(item);

                    if (!string.IsNullOrEmpty(selectedDevice) && string.Equals(m.DeviceName, selectedDevice, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIdx = i;
                    }
                    else if (string.IsNullOrEmpty(selectedDevice) && m.IsPrimary)
                    {
                        selectedIdx = i;
                    }
                }

                if (SpecificMonitorCombo.Items.Count > 0)
                {
                    SpecificMonitorCombo.SelectedIndex = selectedIdx;
                }
            }

            // Populate Visual Display Cards
            if (MonitorsCardsContainer != null)
            {
                MonitorsCardsContainer.Children.Clear();

                foreach (var mon in monitors)
                {
                    var cardBorder = new Border
                    {
                        Background = (Brush)FindResource("SearchBarBgBrush"),
                        BorderBrush = mon.HasCursor ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("CardBorderBrush"),
                        BorderThickness = new Thickness(mon.HasCursor ? 1.5 : 1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12, 10, 12, 10),
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    var cardGrid = new Grid();
                    cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                    // Title row
                    var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                    titleRow.Children.Add(new TextBlock
                    {
                        Text = "🖥️",
                        FontSize = 13,
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    titleRow.Children.Add(new TextBlock
                    {
                        Text = mon.DisplayName,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("TextPrimaryBrush"),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    titleRow.Children.Add(new TextBlock
                    {
                        Text = $"[{mon.DeviceName}]",
                        FontSize = 10,
                        Foreground = (Brush)FindResource("TextMutedBrush"),
                        Margin = new Thickness(6, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    if (mon.IsPrimary)
                    {
                        var primaryBadge = new Border
                        {
                            Background = (Brush)FindResource("AccentBrush"),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(4, 1, 4, 1),
                            Margin = new Thickness(8, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock
                            {
                                Text = "Màn hình chính",
                                FontSize = 8.5,
                                FontWeight = FontWeights.Bold,
                                Foreground = Brushes.White
                            }
                        };
                        titleRow.Children.Add(primaryBadge);
                    }

                    if (mon.HasCursor)
                    {
                        var cursorBadge = new Border
                        {
                            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)), // Emerald #22C55E
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(4, 1, 4, 1),
                            Margin = new Thickness(6, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock
                            {
                                Text = "🖱️ Con trỏ tại đây",
                                FontSize = 8.5,
                                FontWeight = FontWeights.Bold,
                                Foreground = Brushes.White
                            }
                        };
                        titleRow.Children.Add(cursorBadge);
                    }

                    infoStack.Children.Add(titleRow);

                    // Subtitle specs row
                    var specsText = new TextBlock
                    {
                        Text = $"Độ phân giải: {mon.ResolutionText} • Khả dụng: {mon.WorkingAreaText} • Tỷ lệ DPI: {mon.DpiPercentText}",
                        FontSize = 10,
                        Foreground = (Brush)FindResource("TextMutedBrush")
                    };
                    infoStack.Children.Add(specsText);

                    Grid.SetColumn(infoStack, 0);
                    cardGrid.Children.Add(infoStack);

                    // Right action button: "Hiển thị thử"
                    var testBtn = new Button
                    {
                        Content = "🎯 Thử hiển thị",
                        Padding = new Thickness(10, 4, 10, 4),
                        FontSize = 10.5,
                        FontWeight = FontWeights.SemiBold,
                        Background = (Brush)FindResource("CardBgBrush"),
                        Foreground = (Brush)FindResource("AccentBrush"),
                        BorderBrush = (Brush)FindResource("CardBorderBrush"),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand,
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = $"Hiển thị launcher ngay lập tức trên {mon.DisplayName}"
                    };

                    string devName = mon.DeviceName;
                    testBtn.Click += (s, e) =>
                    {
                        _onTestMonitor?.Invoke(devName);
                    };

                    Grid.SetColumn(testBtn, 1);
                    cardGrid.Children.Add(testBtn);

                    cardBorder.Child = cardGrid;
                    MonitorsCardsContainer.Children.Add(cardBorder);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsWindow] Error refreshing monitors: {ex.Message}");
        }
    }

    #endregion
}
