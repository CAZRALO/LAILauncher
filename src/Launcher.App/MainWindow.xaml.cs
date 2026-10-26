using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Launcher.SDK;
using Launcher.Core.Search;
using Launcher.Core.Actions;
using Launcher.Database.Repositories;
using Launcher.App.Services;
using Launcher.AI;

namespace Launcher.App;

public partial class MainWindow : Window
{
    private readonly QueryPipeline _pipeline;
    private readonly ActionRunner _actionRunner;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAIProvider? _aiProvider;
    private readonly AIPlugin? _aiPlugin;
    private readonly OllamaProcessService? _ollamaProcessService;
    private readonly GlobalHotkey _hotkey;
    private readonly DispatcherTimer _clockTimer;
    private CancellationTokenSource? _searchCts;
    private SettingsWindow? _activeSettingsWindow;
    private MonitorTargetMode _monitorTargetMode = MonitorTargetMode.Cursor;
    private string? _specificMonitorDevice = null;
    private WindowVerticalPosition _windowPositionMode = WindowVerticalPosition.TopCenter;
    private bool _hasCustomPosition = false;
    private double _customPositionX = 0;
    private double _customPositionY = 0;

    public MainWindow(QueryPipeline pipeline, ActionRunner actionRunner, ISettingsRepository settingsRepo, IAIProvider? aiProvider = null, AIPlugin? aiPlugin = null, OllamaProcessService? ollamaProcessService = null)
    {
        InitializeComponent();
        _pipeline = pipeline;
        _actionRunner = actionRunner;
        _settingsRepo = settingsRepo;
        _aiProvider = aiProvider;
        _aiPlugin = aiPlugin;
        _ollamaProcessService = ollamaProcessService;
        _hotkey = new GlobalHotkey();

        if (_aiPlugin != null)
        {
            _aiPlugin.OnRequestOpenAiWindow = (prompt, model) => OpenAiStreamingWindow(prompt, model);
        }

        // Clock / Date Timer - Updates date & time every second
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => UpdateDateTimeDisplay();
        _clockTimer.Start();
        UpdateDateTimeDisplay();

        Loaded += OnLoaded;
        Closing += OnClosing;
        ThemeManager.AppearanceChanged += OnAppearanceChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);

        // 0. Enable Windows Hardware Blur (Acrylic / BlurBehind)
        ThemeManager.EnableWindowBlur(this);

        // 1. Load & Apply Saved Theme, Glass Appearance & Accent
        var savedTheme = await _settingsRepo.GetAsync("AppTheme");
        var themeMode = ThemeMode.Dark;
        if (!string.IsNullOrEmpty(savedTheme) && Enum.TryParse<ThemeMode>(savedTheme, true, out var parsedTheme))
        {
            themeMode = parsedTheme;
        }

        var savedOsStyle = await _settingsRepo.GetAsync("AppOsStyle");
        var osStyle = OsStyle.Windows;
        if (!string.IsNullOrEmpty(savedOsStyle) && Enum.TryParse<OsStyle>(savedOsStyle, true, out var parsedOs))
        {
            osStyle = parsedOs;
        }

        var savedForcedLight = await _settingsRepo.GetAsync("AppForcedLightMode");
        bool? forcedLight = null;
        if (!string.IsNullOrEmpty(savedForcedLight) && bool.TryParse(savedForcedLight, out var fl))
        {
            forcedLight = fl;
        }

        var savedAccent = await _settingsRepo.GetAsync("AppAccentColor");
        var savedUseWinAccent = await _settingsRepo.GetAsync("AppAccentFromWindows");
        bool useWindowsAccent = bool.TryParse(savedUseWinAccent, out var uwa) && uwa;

        var savedAccentOnBorder = await _settingsRepo.GetAsync("AppAccentOnBorder");
        bool accentOnBorder = bool.TryParse(savedAccentOnBorder, out var aob) && aob;

        var savedGlassEnabled = await _settingsRepo.GetAsync("AppGlassEnabled");
        bool glassEnabled = bool.TryParse(savedGlassEnabled, out var ge) && ge;

        var savedGlassTint = await _settingsRepo.GetAsync("AppGlassTintColor");

        var savedGlassOpacityStr = await _settingsRepo.GetAsync("AppGlassOpacity");
        double glassOpacity = 0.85;
        if (!string.IsNullOrWhiteSpace(savedGlassOpacityStr) && 
            double.TryParse(savedGlassOpacityStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedOpacity))
        {
            glassOpacity = parsedOpacity;
        }

        var savedGlassTintWithAccent = await _settingsRepo.GetAsync("AppGlassTintWithAccent");
        bool glassTintWithAccent = bool.TryParse(savedGlassTintWithAccent, out var gtwa) && gtwa;

        ThemeManager.LoadConfiguration(
            theme: themeMode,
            accentHex: savedAccent,
            useWindowsAccent: useWindowsAccent,
            accentOnBorder: accentOnBorder,
            glassEnabled: glassEnabled,
            glassTintHex: savedGlassTint,
            glassOpacity: glassOpacity,
            glassTintWithAccent: glassTintWithAccent,
            osStyle: osStyle,
            forcedLightMode: forcedLight
        );

        // 2. Load & Register Hotkey
        var savedHotkey = await _settingsRepo.GetAsync("AppHotkey");
        var modifiers = ModifierKeys.Alt;
        var key = Key.Space;

        if (!string.IsNullOrEmpty(savedHotkey) && GlobalHotkey.TryParseHotkey(savedHotkey, out var parsedMods, out var parsedKey))
        {
            modifiers = parsedMods;
            key = parsedKey;
        }

        _hotkey.Register(helper.Handle, modifiers, key);
        _hotkey.HotkeyPressed += ToggleVisibility;

        UpdateHotkeyBadge(GlobalHotkey.FormatHotkey(_hotkey.Modifiers, _hotkey.Key));
        UpdateOsAndModeUI();
        UpdateEnergySaverUI(Launcher.Core.Utilities.WindowsControlService.IsBatterySaverActive());

        SearchBox.Focus();
        ShowInitialFeatureGuide();

        // 3. Load Monitor Target & Positioning Mode
        var savedMonModeStr = await _settingsRepo.GetAsync("AppMonitorTargetMode");
        if (!string.IsNullOrEmpty(savedMonModeStr) && Enum.TryParse<MonitorTargetMode>(savedMonModeStr, true, out var parsedMonMode))
        {
            _monitorTargetMode = parsedMonMode;
        }

        _specificMonitorDevice = await _settingsRepo.GetAsync("AppMonitorSpecificDevice");

        var savedPosModeStr = await _settingsRepo.GetAsync("AppWindowPositionMode");
        if (!string.IsNullOrEmpty(savedPosModeStr) && Enum.TryParse<WindowVerticalPosition>(savedPosModeStr, true, out var parsedPosMode))
        {
            _windowPositionMode = parsedPosMode;
        }

        // 3.1 Load Saved Window Custom Position
        var savedCustomPosEnabled = await _settingsRepo.GetAsync("AppWindowCustomPositionEnabled");
        _hasCustomPosition = bool.TryParse(savedCustomPosEnabled, out var cpe) && cpe;

        var savedPosX = await _settingsRepo.GetAsync("AppWindowCustomPositionX");
        var savedPosY = await _settingsRepo.GetAsync("AppWindowCustomPositionY");

        if (_hasCustomPosition &&
            double.TryParse(savedPosX, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var px) &&
            double.TryParse(savedPosY, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var py))
        {
            _customPositionX = px;
            _customPositionY = py;
        }
        else
        {
            _hasCustomPosition = false;
        }

        MonitorService.PositionWindow(this, _monitorTargetMode, _specificMonitorDevice, _windowPositionMode, _hasCustomPosition, _customPositionX, _customPositionY);
        UpdateResetPositionButtonState();
    }

    private void UpdateDateTimeDisplay()
    {
        var now = DateTime.Now;
        if (HeaderClockText != null)
        {
            HeaderClockText.Text = now.ToString("dd-MM • HH:mm");
        }
        if (GnomeClockText != null)
        {
            GnomeClockText.Text = now.ToString("ddd, dd/MM  HH:mm");
        }
    }

    public void UpdateHotkeyBadge(string hotkeyText)
    {
        // Hotkey badge removed from search bar for minimal visual clutter
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _clockTimer.Stop();
        _hotkey.Dispose();
    }

    private DateTime _lastToggleTime = DateTime.MinValue;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    public void ToggleVisibility()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastToggleTime).TotalMilliseconds < 250)
        {
            return;
        }
        _lastToggleTime = now;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ToggleVisibility);
            return;
        }

        if (IsVisible)
        {
            Hide();
        }
        else
        {
            ShowLauncher();
        }
    }

    public void ShowLauncher()
    {
        MonitorService.PositionWindow(this, _monitorTargetMode, _specificMonitorDevice, _windowPositionMode, _hasCustomPosition, _customPositionX, _customPositionY);

        Show();
        WindowState = WindowState.Normal;
        Topmost = true;
        Activate();
        Focus();

        SearchBox.Focus();
        SearchBox.SelectAll();

        var helper = new WindowInteropHelper(this);
        ForceForeground(helper.Handle);

        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            ShowInitialFeatureGuide();
        }

        UpdateEnergySaverUI(Launcher.Core.Utilities.WindowsControlService.IsBatterySaverActive());
    }

    private static void ForceForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;

        try
        {
            var foreWnd = GetForegroundWindow();
            if (foreWnd != hWnd && foreWnd != IntPtr.Zero)
            {
                var curThread = GetCurrentThreadId();
                var foreThread = GetWindowThreadProcessId(foreWnd, IntPtr.Zero);
                if (curThread != foreThread && foreThread != 0)
                {
                    AttachThreadInput(curThread, foreThread, true);
                    BringWindowToTop(hWnd);
                    SetForegroundWindow(hWnd);
                    AttachThreadInput(curThread, foreThread, false);
                    return;
                }
            }

            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }
        catch
        {
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1. If ContextMenu is open, let it handle arrow keys, Enter, and Escape
        if (ResultsContextMenu.IsOpen)
        {
            if (e.Key == Key.Escape)
            {
                ResultsContextMenu.IsOpen = false;
                e.Handled = true;
            }
            return;
        }

        // 1.5 If ActionFlyoutPanel is open, handle Escape to close it
        if (ActionFlyoutPanel != null && ActionFlyoutPanel.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                ActionFlyoutPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }
        }

        // 2. Escape: Hide launcher window
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        // 2.2 Alt + Space: Toggle/Hide launcher and prevent Windows System Menu
        var isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        var altKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (isAlt && altKey == Key.Space)
        {
            Hide();
            e.Handled = true;
            return;
        }

        // 2.5 Alt + 1..9: Activate quick shortcut item
        if (isAlt)
        {
            var digit = -1;
            if (altKey >= Key.D1 && altKey <= Key.D9)
            {
                digit = altKey - Key.D1;
            }
            else if (altKey >= Key.NumPad1 && altKey <= Key.NumPad9)
            {
                digit = altKey - Key.NumPad1;
            }

            if (digit >= 0 && digit < ResultsList.Items.Count)
            {
                ResultsList.SelectedIndex = digit;
                ExecuteSelectedResult();
                e.Handled = true;
                return;
            }
        }
        // 3. Ctrl + ,: Open Settings window
        else if (e.Key == Key.OemComma && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            OpenSettingsWindow();
            e.Handled = true;
        }
        // 3.5 Ctrl + D: Toggle Light/Dark mode (matching web shortcut)
        else if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ThemeManager.ToggleLightDarkMode();
            _ = _settingsRepo.SetAsync("AppForcedLightMode", ThemeManager.IsLightMode.ToString());
            UpdateOsAndModeUI();
            e.Handled = true;
        }
        // 3.6 Ctrl + Alt + S: Summarize selected document with AI
        else if (e.Key == Key.S && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            OnActionSummarizeFileClicked(this, new RoutedEventArgs());
            e.Handled = true;
        }
        // 4. Down Arrow: Navigate to next result in list
        else if (e.Key == Key.Down)
        {
            if (ResultsList.Items.Count > 0)
            {
                var nextIndex = (ResultsList.SelectedIndex + 1) % ResultsList.Items.Count;
                ResultsList.SelectedIndex = nextIndex;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            }
            e.Handled = true;
        }
        // 5. Up Arrow: Navigate to previous result in list (or wrap to bottom)
        else if (e.Key == Key.Up)
        {
            if (ResultsList.Items.Count > 0)
            {
                var prevIndex = ResultsList.SelectedIndex <= 0 ? ResultsList.Items.Count - 1 : ResultsList.SelectedIndex - 1;
                ResultsList.SelectedIndex = prevIndex;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            }
            e.Handled = true;
        }
        // 6. PageDown: Jump down 5 items
        else if (e.Key == Key.PageDown)
        {
            if (ResultsList.Items.Count > 0)
            {
                var nextIndex = Math.Min(ResultsList.SelectedIndex + 5, ResultsList.Items.Count - 1);
                ResultsList.SelectedIndex = nextIndex;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            }
            e.Handled = true;
        }
        // 7. PageUp: Jump up 5 items
        else if (e.Key == Key.PageUp)
        {
            if (ResultsList.Items.Count > 0)
            {
                var prevIndex = Math.Max(ResultsList.SelectedIndex - 5, 0);
                ResultsList.SelectedIndex = prevIndex;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            }
            e.Handled = true;
        }
        // 8. Enter: Execute selected result (with modifier combinations)
        else if (e.Key == Key.Enter)
        {
            var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isCtrl && isShift)
            {
                ExecuteSelectedRunAsAdmin();
            }
            else if (isCtrl)
            {
                ExecuteSelectedOpenFolder();
            }
            else
            {
                ExecuteSelectedResult();
            }
            e.Handled = true;
        }
        // 9. Tab: Toggle Action Flyout Menu (matching web action-flyout-menu)
        else if (e.Key == Key.Tab)
        {
            if (ActionFlyoutPanel != null)
            {
                if (ActionFlyoutPanel.Visibility == Visibility.Visible)
                {
                    ActionFlyoutPanel.Visibility = Visibility.Collapsed;
                }
                else if (ResultsList.SelectedItem is QueryResult selected)
                {
                    FlyoutTargetTitle.Text = selected.Title;
                    ActionFlyoutPanel.Visibility = Visibility.Visible;
                }
            }
            e.Handled = true;
        }
        // 10. Right Arrow: Autocomplete folder path only if cursor is at the end of SearchBox text
        else if (e.Key == Key.Right)
        {
            if (SearchBox.IsKeyboardFocused && SearchBox.CaretIndex < SearchBox.Text.Length)
            {
                return;
            }

            if (ResultsList.SelectedItem is QueryResult selected && selected.Type == ResultType.Folder)
            {
                var targetPath = selected.Metadata.TryGetValue("Path", out var metaPath) ? metaPath : selected.Subtitle;
                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    var folderPath = targetPath.EndsWith("\\") || targetPath.EndsWith("/") ? targetPath : targetPath + "\\";
                    SearchBox.Text = folderPath;
                    SearchBox.CaretIndex = SearchBox.Text.Length;
                    e.Handled = true;
                }
            }
        }
        // 11. Ctrl + C: Copy selected item path if no text in SearchBox is selected
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (SearchBox.SelectionLength == 0 && ResultsList.SelectedItem != null)
            {
                CopySelectedPath();
                e.Handled = true;
            }
        }
        // 12. If results list is focused and user types normal keys or Backspace, redirect focus to SearchBox
        else if (!SearchBox.IsKeyboardFocused && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift))
        {
            if ((e.Key >= Key.A && e.Key <= Key.Z) ||
                (e.Key >= Key.D0 && e.Key <= Key.D9) ||
                (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) ||
                e.Key == Key.Back || e.Key == Key.Space ||
                e.Key == Key.OemMinus || e.Key == Key.OemPlus || e.Key == Key.OemPeriod ||
                e.Key == Key.OemComma || e.Key == Key.OemQuestion || e.Key == Key.OemQuotes ||
                e.Key == Key.Oem1 || e.Key == Key.OemOpenBrackets || e.Key == Key.OemCloseBrackets)
            {
                SearchBox.Focus();
                SearchBox.CaretIndex = SearchBox.Text.Length;
            }
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // Don't auto-hide if settings window is open
        if (_activeSettingsWindow != null && _activeSettingsWindow.IsLoaded)
        {
            return;
        }

        Hide();
    }

    private void OnClearSearchClicked(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        UpdateSearchPrefixBadge(string.Empty);
        SearchBox.Focus();
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (ActionFlyoutPanel != null && ActionFlyoutPanel.Visibility == Visibility.Visible)
        {
            ActionFlyoutPanel.Visibility = Visibility.Collapsed;
        }

        var query = SearchBox.Text;
        PlaceholderText.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
        ClearSearchButton.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;
        UpdateSearchPrefixBadge(query);

        if (string.IsNullOrWhiteSpace(query))
        {
            _searchCts?.Cancel();
            ShowInitialFeatureGuide();
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            // Debounce 40ms
            await Task.Delay(40, token);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = await _pipeline.ExecuteAsync(query, token);
            sw.Stop();

            if (!token.IsCancellationRequested)
            {
                var resultList = results.ToList();

                // Quick Theme presets integration
                if (IsThemeQuery(query))
                {
                    var qFilter = query.Trim().ToLowerInvariant()
                        .Replace("theme:", "").Replace("mau:", "").Replace("màu:", "")
                        .Replace("theme ", "").Replace("mau ", "").Replace("màu ", "").Trim();

                    var matchedThemes = string.IsNullOrEmpty(qFilter) || qFilter is "theme" or "mau" or "màu"
                        ? QuickThemes
                        : QuickThemes.Where(t => t.Title.Contains(qFilter, StringComparison.OrdinalIgnoreCase) || 
                                                t.Subtitle.Contains(qFilter, StringComparison.OrdinalIgnoreCase) || 
                                                t.Mode.ToString().Contains(qFilter, StringComparison.OrdinalIgnoreCase)).ToArray();

                    foreach (var th in matchedThemes.Reverse())
                    {
                        var themeResult = new QueryResult
                        {
                            Id = th.Id,
                            Title = th.Title,
                            Subtitle = th.Subtitle,
                            Type = ResultType.Setting,
                            Score = 950,
                            Actions = new List<ResultAction>
                            {
                                new ResultAction
                                {
                                    Name = "Áp dụng Theme",
                                    SyncAction = () => ApplyPresetTheme(th.Mode, th.Title)
                                }
                            }
                        };
                        resultList.Insert(0, themeResult);
                    }
                }

                // Quick Settings launcher integration
                if (IsSettingsQuery(query))
                {
                    var settingsResult = new QueryResult
                    {
                        Id = "settings_launcher",
                        Title = "Cài đặt hệ thống (Settings)",
                        Subtitle = "Tùy chỉnh Theme (9 Web Themes / Dark / Light / Glass), Phím tắt mở app, Khởi động cùng Windows",
                        Type = ResultType.Setting,
                        Score = 1000,
                        Actions = new List<ResultAction>
                        {
                            new ResultAction
                            {
                                Name = "Open Settings",
                                SyncAction = OpenSettingsWindow
                            }
                        }
                    };
                    resultList.Insert(0, settingsResult);
                }

                ResultsList.ItemsSource = resultList;
                if (resultList.Count > 0)
                {
                    ResultsList.SelectedIndex = 0;
                    ResultsList.Visibility = Visibility.Visible;
                    NoResultsPanel.Visibility = Visibility.Collapsed;
                    StatusDot.Fill = (Brush)new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                }
                else
                {
                    ResultsList.Visibility = Visibility.Collapsed;
                    NoResultsPanel.Visibility = Visibility.Visible;
                    StatusDot.Fill = (Brush)new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                }

                StatusText.Text = resultList.Count > 0
                    ? $"{resultList.Count} kết quả trong {sw.ElapsedMilliseconds} ms"
                    : "Không tìm thấy kết quả phù hợp";
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore canceled debounce
        }
    }

    private void ShowInitialFeatureGuide()
    {
        var guideItems = GetInitialFeatureGuide();
        NoResultsPanel.Visibility = Visibility.Collapsed;
        ResultsList.Visibility = Visibility.Visible;
        ClearSearchButton.Visibility = Visibility.Collapsed;
        StatusDot.Fill = (Brush)new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        StatusText.Text = "Local AI Launcher • Sẵn sàng";
        ResultsList.ItemsSource = guideItems;
        if (guideItems.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
        }
    }

    private static List<QueryResult> GetInitialFeatureGuide()
    {
        return
        [
            new QueryResult
            {
                Id = "guide_calc",
                Title = "calc",
                Subtitle = "Tính toán số học, đổi đơn vị và hệ cơ số (vd: 50 * 20%, 100 km to mi, 0xFF to dec)",
                Type = ResultType.Calculator,
                ShortcutHint = "Alt+1",
                Metadata = new() { ["ActionKeyword"] = "calc" }
            },
            new QueryResult
            {
                Id = "guide_ai",
                Title = "ai",
                Subtitle = "Hỏi đáp AI, tóm tắt hoặc dịch Clipboard với mô hình cục bộ (ai: câu hỏi, ? prompt)",
                Type = ResultType.AI,
                ShortcutHint = "Alt+2",
                Metadata = new() { ["ActionKeyword"] = "ai" }
            },
            new QueryResult
            {
                Id = "guide_shell",
                Title = ">",
                Subtitle = "Chạy lệnh Terminal trực tiếp (CMD / PowerShell: ping, ipconfig, code .)",
                Type = ResultType.Command,
                ShortcutHint = "Alt+3",
                Metadata = new() { ["ActionKeyword"] = ">" }
            },
            new QueryResult
            {
                Id = "guide_bookmarks",
                Title = "b",
                Subtitle = "Tìm kiếm dấu trang trình duyệt web (Chrome, Edge, Brave, Firefox...)",
                Type = ResultType.Bookmark,
                ShortcutHint = "Alt+4",
                Metadata = new() { ["ActionKeyword"] = "b" }
            },
            new QueryResult
            {
                Id = "guide_sys",
                Title = "sys",
                Subtitle = "Điều khiển Windows (Chế độ sáng/tối, âm lượng, độ sáng màn hình, pin, Wi-Fi)",
                Type = ResultType.SystemCommand,
                ShortcutHint = "Alt+5",
                Metadata = new() { ["ActionKeyword"] = "sys" }
            },
            new QueryResult
            {
                Id = "guide_settings",
                Title = "set",
                Subtitle = "Mở nhanh trang Cài đặt Windows (Display, Sound, Network, Bluetooth, Apps...)",
                Type = ResultType.Setting,
                ShortcutHint = "Alt+6",
                Metadata = new() { ["ActionKeyword"] = "set" }
            },
            new QueryResult
            {
                Id = "guide_proc",
                Title = "kill",
                Subtitle = "Quản lý và dừng ứng dụng hoặc tiến trình đang chạy (Process Killer)",
                Type = ResultType.Process,
                ShortcutHint = "Alt+7",
                Metadata = new() { ["ActionKeyword"] = "kill" }
            },
            new QueryResult
            {
                Id = "guide_google",
                Title = "g",
                Subtitle = "Tìm kiếm nhanh từ khóa trên Google Search",
                Type = ResultType.Url,
                ShortcutHint = "Alt+8",
                Metadata = new() { ["ActionKeyword"] = "g" }
            },
            new QueryResult
            {
                Id = "guide_youtube",
                Title = "yt",
                Subtitle = "Tìm kiếm video và kênh trên YouTube",
                Type = ResultType.Url,
                ShortcutHint = "Alt+9",
                Metadata = new() { ["ActionKeyword"] = "yt" }
            },
            new QueryResult
            {
                Id = "guide_translate",
                Title = "trans",
                Subtitle = "Dịch nhanh đoạn văn bản qua Google Translate",
                Type = ResultType.Url,
                Metadata = new() { ["ActionKeyword"] = "trans" }
            },
            new QueryResult
            {
                Id = "guide_github",
                Title = "gh",
                Subtitle = "Tra cứu mã nguồn, thư viện và repository trên GitHub",
                Type = ResultType.Url,
                Metadata = new() { ["ActionKeyword"] = "gh" }
            },
            new QueryResult
            {
                Id = "guide_launcher_settings",
                Title = "settings",
                Subtitle = "Tùy chỉnh giao diện, phím tắt và mô hình AI của Launcher",
                Type = ResultType.Plugin,
                ShortcutHint = "Ctrl+,",
                Metadata = new() { ["ActionKeyword"] = "settings" }
            },
            new QueryResult
            {
                Id = "guide_semantic",
                Title = "🎯 AI Semantic Search",
                Subtitle = "Tìm kiếm ngữ nghĩa không cần nhớ tên tệp (vd: 'báo cáo tài chính', 'hợp đồng', 'ảnh hoàng hôn')",
                Type = ResultType.AI,
                ShortcutHint = "NLP",
                Metadata = new() { ["ActionKeyword"] = "báo cáo tài chính" }
            },
            new QueryResult
            {
                Id = "guide_filters",
                Title = "pdf: / doc: / img: / code:",
                Subtitle = "Bộ lọc định dạng thông minh cho tài liệu, bảng tính (sheet:), hình ảnh, mã nguồn (code:)",
                Type = ResultType.Folder,
                Metadata = new() { ["ActionKeyword"] = "doc:" }
            },
            new QueryResult
            {
                Id = "guide_explorer",
                Title = "doc:",
                Subtitle = "Duyệt nhanh tài liệu và cây thư mục tập tin cục bộ",
                Type = ResultType.Folder,
                Metadata = new() { ["ActionKeyword"] = "doc:" }
            }
        ];
    }

    private static readonly (string Id, string Title, string Subtitle, ThemeMode Mode, string ColorHex)[] QuickThemes = new[]
    {
        ("theme_winfluent", "Theme: Windows Fluent Blue", "Màu xanh dương Fluent (#3B82F6) phong cách Windows 11", ThemeMode.Dark, "#3B82F6"),
        ("theme_ubuntu", "Theme: Ubuntu Yaru Orange", "Màu cam Yaru (#E95420) phong cách Ubuntu Linux GNOME", ThemeMode.Ubuntu, "#E95420"),
        ("theme_arch", "Theme: Arch / Fedora Cyan", "Màu xanh lơ Cyan (#00AAD4) phong cách Arch & Fedora Linux", ThemeMode.Arch, "#00AAD4"),
        ("theme_aurora", "Theme: Liquid Aurora Glass", "Màu tím Aurora (#8B5CF6) kết hợp kính mờ đa sắc", ThemeMode.Aurora, "#8B5CF6"),
        ("theme_cyberpunk", "Theme: Cyberpunk Neon", "Màu hồng neon Cyberpunk (#F43F5E) sắc sảo cá tính", ThemeMode.Cyberpunk, "#F43F5E"),
        ("theme_nord", "Theme: Nordic Frost Slate", "Màu băng tuyết Bắc Âu (#88C0D0) thanh thoát tối giản", ThemeMode.Nord, "#88C0D0"),
        ("theme_dracula", "Theme: Dracula Nocturne", "Màu tím dạ thảo Dracula (#BD93F9) huyền bí thanh lịch", ThemeMode.Dracula, "#BD93F9"),
        ("theme_emerald", "Theme: Emerald Jade Mint", "Màu xanh ngọc bích (#10B981) dịu mát thư thái", ThemeMode.Emerald, "#10B981"),
        ("theme_sunset", "Theme: Sunset Dusk Coral", "Màu hoàng hôn Sunset (#FB923C) ấm áp quyến rũ", ThemeMode.Sunset, "#FB923C"),
    };

    private static bool IsThemeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;
        var q = query.Trim().ToLowerInvariant();
        return q is "theme" or "themes" or "mau" or "màu" or "theme:" or "mau:" or "màu:"
            || q.StartsWith("theme ") || q.StartsWith("mau ") || q.StartsWith("màu ");
    }

    private async void ApplyPresetTheme(ThemeMode mode, string name)
    {
        ThemeManager.ApplyTheme(mode);
        await _settingsRepo.SetAsync("AppTheme", mode.ToString());
        await _settingsRepo.SetAsync("AppAccentColor", string.Empty);
        await _settingsRepo.SetAsync("AppGlassTintColor", string.Empty);
        ShowToast($"Đã áp dụng {name}!");
    }

    private static bool IsSettingsQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;
        var q = query.Trim().ToLowerInvariant();
        return q is "settings" or "cai dat" or "cài đặt" or "giao dien" or "giao diện" 
                 or "hotkey" or "phim tat" or "phím tắt" or "khoi dong" or "khởi động" or ">settings" or ">config";
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelectedResult();
    }

    private async void ExecuteSelectedResult()
    {
        if (ActionFlyoutPanel != null)
        {
            ActionFlyoutPanel.Visibility = Visibility.Collapsed;
        }

        if (ResultsList.SelectedItem is QueryResult selected)
        {
            if (selected.Id == "calc_prompt" || selected.Id == "cmd_prompt")
            {
                return;
            }

            if (selected.Type == ResultType.Calculator)
            {
                Clipboard.SetText(selected.Title);
                StatusText.Text = $"📋 Đã sao chép kết quả: {selected.Title}";
                Hide();
                return;
            }

            if (selected.Type == ResultType.AI)
            {
                Hide();
                if (selected.Metadata.TryGetValue("Prompt", out var prompt))
                {
                    var model = selected.Metadata.TryGetValue("Model", out var m) ? m : _aiProvider?.DefaultModel;
                    OpenAiStreamingWindow(prompt, model);
                    return;
                }

                if (selected.PrimaryAction != null)
                {
                    await selected.PrimaryAction.ExecuteAsync();
                    return;
                }
            }

            if (selected.Id.StartsWith("theme_"))
            {
                if (selected.PrimaryAction != null)
                {
                    await selected.PrimaryAction.ExecuteAsync();
                }
                return;
            }

            if (selected.Id == "settings_launcher" || selected.Id == "guide_launcher_settings")
            {
                OpenSettingsWindow();
                return;
            }

            if (selected.Metadata.TryGetValue("ActionKeyword", out var kw))
            {
                SearchBox.Text = kw.EndsWith(":") ? kw : kw + " ";
                SearchBox.CaretIndex = SearchBox.Text.Length;
                SearchBox.Focus();
                return;
            }

            Hide();
            await _actionRunner.ExecuteAsync(SearchBox.Text, selected);
        }
    }

    private async void ExecuteSelectedRunAsAdmin()
    {
        if (ResultsList.SelectedItem is QueryResult selected)
        {
            if (selected.Id == "cmd_prompt" || selected.Id == "calc_prompt")
            {
                return;
            }

            if (selected.Metadata.ContainsKey("ActionKeyword"))
            {
                ExecuteSelectedResult();
                return;
            }

            Hide();
            await _actionRunner.ExecuteRunAsAdminAsync(SearchBox.Text, selected);
        }
    }

    private async void ExecuteSelectedOpenFolder()
    {
        if (ResultsList.SelectedItem is QueryResult selected)
        {
            if (selected.Metadata.ContainsKey("ActionKeyword"))
            {
                ExecuteSelectedResult();
                return;
            }

            var folderAction = selected.Actions.FirstOrDefault(a => a.Name.Contains("Folder", StringComparison.OrdinalIgnoreCase));
            if (folderAction != null)
            {
                Hide();
                await _actionRunner.ExecuteAsync(SearchBox.Text, selected, folderAction);
            }
            else
            {
                var targetPath = selected.Metadata.TryGetValue("Path", out var metaPath) ? metaPath : selected.Subtitle;
                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    Hide();
                    ActionRunner.CreateOpenFolderAction(targetPath).SyncAction?.Invoke();
                }
            }
        }
    }

    private void CopySelectedPath()
    {
        if (ResultsList.SelectedItem is QueryResult selected)
        {
            if (selected.Type == ResultType.Calculator)
            {
                Clipboard.SetText(selected.Title);
                StatusText.Text = $"📋 Đã sao chép kết quả: {selected.Title}";
                return;
            }

            if (selected.Type == ResultType.Command && selected.Metadata.TryGetValue("Command", out var cmd))
            {
                Clipboard.SetText(cmd);
                StatusText.Text = $"📋 Đã sao chép lệnh: {cmd}";
                return;
            }

            if (selected.Type == ResultType.Url && selected.Metadata.TryGetValue("Url", out var url))
            {
                Clipboard.SetText(url);
                StatusText.Text = $"📋 Đã sao chép liên kết: {url}";
                return;
            }

            var targetPath = selected.Metadata.TryGetValue("Path", out var metaPath) ? metaPath : selected.Subtitle;
            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                Clipboard.SetText(targetPath);
                StatusText.Text = $"📋 Đã sao chép đường dẫn: {targetPath}";
            }
        }
    }

    public void OpenAiStreamingWindow(string prompt, string? model = null)
    {
        if (_aiProvider == null) return;
        var aiWindow = new AIStreamingWindow(_aiProvider, prompt, model)
        {
            Owner = this
        };
        aiWindow.Show();
    }

    public void OpenSettingsWindow()
    {
        if (_activeSettingsWindow != null && _activeSettingsWindow.IsLoaded)
        {
            _activeSettingsWindow.Activate();
            return;
        }

        _activeSettingsWindow = new SettingsWindow(
            _settingsRepo,
            _hotkey,
            UpdateHotkeyBadge,
            _aiProvider,
            _ollamaProcessService,
            ApplyMonitorSettings,
            TestPositionOnMonitor)
        {
            Owner = this
        };
        _activeSettingsWindow.Closed += (s, e) => _activeSettingsWindow = null;
        _activeSettingsWindow.ShowDialog();
    }

    public void ApplyMonitorSettings(MonitorTargetMode mode, string? specificDevice, WindowVerticalPosition vPos)
    {
        _monitorTargetMode = mode;
        _specificMonitorDevice = specificDevice;
        _windowPositionMode = vPos;

        if (IsVisible)
        {
            MonitorService.PositionWindow(this, _monitorTargetMode, _specificMonitorDevice, _windowPositionMode, _hasCustomPosition, _customPositionX, _customPositionY);
        }
    }

    public void TestPositionOnMonitor(string deviceName)
    {
        MonitorService.PositionWindow(this, MonitorTargetMode.Specific, deviceName, _windowPositionMode);
        Show();
        WindowState = WindowState.Normal;
        Topmost = true;
        Activate();
        Focus();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    private void OnActionOpenClicked(object sender, RoutedEventArgs e)
    {
        ExecuteSelectedResult();
    }

    private void OnActionRunAsAdminClicked(object sender, RoutedEventArgs e)
    {
        ExecuteSelectedRunAsAdmin();
    }

    private void OnActionOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        ExecuteSelectedOpenFolder();
    }

    private void OnActionCopyPathClicked(object sender, RoutedEventArgs e)
    {
        CopySelectedPath();
    }

    private void OnActionSummarizeFileClicked(object sender, RoutedEventArgs e)
    {
        if (ActionFlyoutPanel != null)
        {
            ActionFlyoutPanel.Visibility = Visibility.Collapsed;
        }

        if (ResultsList.SelectedItem is QueryResult selected)
        {
            var path = selected.Metadata.TryGetValue("Path", out var p) ? p : selected.Subtitle;
            if (File.Exists(path))
            {
                var content = ReadTextSnippet(path, 6000);
                var prompt = $"Hãy đọc và tóm tắt ngắn gọn các ý chính của tài liệu '{selected.Title}':\n\n```\n{content}\n```";
                OpenAiStreamingWindow(prompt);
                return;
            }

            OpenAiStreamingWindow($"Hãy tóm tắt và phân tích chi tiết về: {selected.Title}");
        }
    }

    private static string ReadTextSnippet(string filePath, int maxChars)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var buffer = new char[maxChars];
            int read = reader.Read(buffer, 0, maxChars);
            return new string(buffer, 0, read);
        }
        catch
        {
            return $"[Tệp: {Path.GetFileName(filePath)}]";
        }
    }

    private void OnActionAskAIClicked(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is QueryResult selected)
        {
            var prompt = $"Giải thích chi tiết về '{selected.Title}' ({selected.Subtitle}) và hướng dẫn sử dụng.";
            OpenAiStreamingWindow(prompt);
        }
        else if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            OpenAiStreamingWindow(SearchBox.Text);
        }
        else
        {
            OpenAiStreamingWindow("Xin chào! Bạn có thể giúp gì cho tôi hôm nay?");
        }
    }

    private void OnAiQuickLaunchClicked(object sender, RoutedEventArgs e)
    {
        var prompt = string.IsNullOrWhiteSpace(SearchBox.Text) ? "Xin chào! Bạn có thể giúp gì cho tôi hôm nay?" : SearchBox.Text;
        OpenAiStreamingWindow(prompt);
    }

    private void OnAppearanceChanged()
    {
        Dispatcher.Invoke(() =>
        {
            UpdateOsAndModeUI();
            if (ResultsList.ItemsSource != null)
            {
                ResultsList.Items.Refresh();
            }
        });
    }

    private void OnWindowCloseClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnWindowMinimizeClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    public void ShowToast(string message)
    {
        if (ToastShelf == null || ToastMessageText == null) return;
        ToastMessageText.Text = message;
        ToastShelf.BeginAnimation(UIElement.OpacityProperty, null);
        ToastShelf.Opacity = 1.0;

        _toastTimer?.Stop();
        _toastTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.0)
        };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250));
            ToastShelf.BeginAnimation(UIElement.OpacityProperty, anim);
        };
        _toastTimer.Start();
    }

    private async void OnOsSwitchClicked(object sender, RoutedEventArgs e)
    {
        var newOs = ThemeManager.CurrentOsStyle == OsStyle.Windows ? OsStyle.Linux : OsStyle.Windows;
        ThemeManager.SetOsStyle(newOs);
        await _settingsRepo.SetAsync("AppOsStyle", newOs.ToString());
        UpdateOsAndModeUI();
        ShowToast(newOs == OsStyle.Windows ? "Đã chuyển sang giao diện Windows 11 Fluent" : "Đã chuyển sang giao diện Linux GNOME Adwaita");
    }

    private async void OnLightDarkToggleClicked(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleLightDarkMode();
        await _settingsRepo.SetAsync("AppForcedLightMode", ThemeManager.IsLightMode.ToString());
        UpdateOsAndModeUI();
        ShowToast(ThemeManager.IsLightMode ? "Đã chuyển sang Chế độ Sáng" : "Đã chuyển sang Chế độ Tối");
    }

    private void UpdateOsAndModeUI()
    {
        bool isWin = ThemeManager.CurrentOsStyle == OsStyle.Windows;

        if (GnomeTopBar != null)
        {
            GnomeTopBar.Visibility = isWin ? Visibility.Collapsed : Visibility.Visible;
        }

        if (HeaderThemeBadge != null)
        {
            HeaderThemeBadge.Text = ThemeManager.CurrentTheme.ToString();
        }

        if (OsIconPath != null && OsLabelText != null)
        {
            OsIconPath.Data = isWin ? (Geometry)FindResource("GeoWindows") : (Geometry)FindResource("GeoLinuxTux");
            OsLabelText.Text = isWin ? "Win 11" : "Linux";
        }

        if (ModeEmojiText != null && ModeLabelText != null)
        {
            bool isLight = ThemeManager.IsLightMode;
            ModeEmojiText.Text = isLight ? "☀️" : "🌙";
            ModeLabelText.Text = isLight ? "Light" : "Dark";
        }
    }

    #region Horizontal Quick Settings Strip Handlers (Windows Search 2025 Inline Toggles)

    private void OnQuickHotspotClicked(object sender, RoutedEventArgs e)
    {
        Launcher.Core.Utilities.WindowsControlService.ToggleMobileHotspot();
        ShowToast("📡 Mobile Hotspot: Đã thực thi bật/tắt trực tiếp không cần mở Settings!");
    }

    private void OnQuickEnergySaverClicked(object sender, RoutedEventArgs e)
    {
        bool isNowOn = Launcher.Core.Utilities.WindowsControlService.ToggleBatterySaver();
        UpdateEnergySaverUI(isNowOn);
        ShowToast(isNowOn ? "🍃 Tiết kiệm pin: Đã BẬT (Energy Saver)" : "🔋 Tiết kiệm pin: Đã TẮT (Balanced Mode)");
    }

    private void UpdateEnergySaverUI(bool isNowOn)
    {
        if (QuickEnergySaverText != null)
        {
            QuickEnergySaverText.Text = isNowOn ? "Tiết kiệm pin (Bật)" : "Tiết kiệm pin";
        }
        if (QuickEnergySaverBtn != null)
        {
            QuickEnergySaverBtn.Opacity = isNowOn ? 1.0 : 0.85;
        }
    }

    private void OnQuickTaskbarAlignClicked(object sender, RoutedEventArgs e)
    {
        var newAlign = Launcher.Core.Utilities.WindowsControlService.ToggleTaskbarAlignment();
        if (QuickTaskbarText != null)
        {
            QuickTaskbarText.Text = newAlign == Launcher.Core.Utilities.TaskbarAlignment.Center ? "Taskbar: Giữa" : "Taskbar: Trái";
        }
        ShowToast(newAlign == Launcher.Core.Utilities.TaskbarAlignment.Center 
            ? "🎯 Đã chuyển thanh Taskbar sang Căn Giữa (Center)" 
            : "◀️ Đã chuyển thanh Taskbar sang Căn Trái (Left)");
    }

    private void OnQuickScreenshotClicked(object sender, RoutedEventArgs e)
    {
        Launcher.Core.Utilities.WindowsControlService.CaptureAndSaveFullScreen();
        ShowToast("📸 Đã chụp màn hình! Ảnh đã lưu vào Pictures\\Screenshots & sao chép vào Clipboard.");
    }

    private void OnQuickScreenshotRightClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Launcher.Core.Utilities.WindowsControlService.LaunchScreenSnipping();
        ShowToast("✂️ Đã mở Windows Snipping Tool (Win+Shift+S) để cắt ảnh");
    }

    private void OnQuickProcessesClicked(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "trinh chay ngam";
        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text.Length;
        ShowToast("⚡ Đang hiển thị danh sách các trình chạy ngầm và mức tiêu thụ RAM");
    }

    private void OnQuickWifiClicked(object sender, RoutedEventArgs e)
    {
        Launcher.Core.Utilities.WindowsControlService.ShowAvailableNetworks();
    }

    private void OnQuickBluetoothClicked(object sender, RoutedEventArgs e)
    {
        Launcher.Core.Utilities.WindowsControlService.ToggleBluetooth();
        ShowToast("📶 Bluetooth: Đã gửi lệnh bật/tắt sóng Bluetooth");
    }

    #endregion

    private void UpdateSearchPrefixBadge(string query)
    {
        if (SearchPrefixBadge == null) return;

        var q = query.TrimStart();
        if (q.StartsWith(">"))
        {
            SearchPrefixBadge.Visibility = Visibility.Visible;
            SearchPrefixIcon.Data = (Geometry)FindResource("GeoTerminal");
            SearchPrefixText.Text = "Lệnh Shell";
            SearchPrefixBadge.Background = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)); // Emerald
        }
        else if (q.StartsWith("calc ", StringComparison.OrdinalIgnoreCase) || IsMathExpression(q))
        {
            SearchPrefixBadge.Visibility = Visibility.Visible;
            SearchPrefixIcon.Data = (Geometry)FindResource("GeoCalculator");
            SearchPrefixText.Text = "Máy tính";
            SearchPrefixBadge.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Amber
        }
        else if (q.StartsWith("ai ", StringComparison.OrdinalIgnoreCase) || q.StartsWith("? "))
        {
            SearchPrefixBadge.Visibility = Visibility.Visible;
            SearchPrefixIcon.Data = (Geometry)FindResource("GeoSparkle");
            SearchPrefixText.Text = "Trợ lý AI";
            SearchPrefixBadge.Background = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7)); // Purple
        }
        else if (q.StartsWith("g ", StringComparison.OrdinalIgnoreCase))
        {
            SearchPrefixBadge.Visibility = Visibility.Visible;
            SearchPrefixIcon.Data = (Geometry)FindResource("GeoGlobe");
            SearchPrefixText.Text = "Google";
            SearchPrefixBadge.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)); // Blue
        }
        else if (q.StartsWith("yt ", StringComparison.OrdinalIgnoreCase))
        {
            SearchPrefixBadge.Visibility = Visibility.Visible;
            SearchPrefixIcon.Data = (Geometry)FindResource("GeoGlobe");
            SearchPrefixText.Text = "YouTube";
            SearchPrefixBadge.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Red
        }
        else if (q.StartsWith("gh ", StringComparison.OrdinalIgnoreCase))
        {
            SearchPrefixBadge.Visibility = Visibility.Visible;
            SearchPrefixIcon.Data = (Geometry)FindResource("GeoGlobe");
            SearchPrefixText.Text = "GitHub";
            SearchPrefixBadge.Background = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1)); // Indigo
        }
        else if (q.StartsWith("sys ", StringComparison.OrdinalIgnoreCase) || q.StartsWith("win ", StringComparison.OrdinalIgnoreCase))
        {
            SearchPrefixBadge.Visibility = Visibility.Visible;
            SearchPrefixIcon.Data = (Geometry)FindResource("GeoSettings");
            SearchPrefixText.Text = "Hệ thống";
            SearchPrefixBadge.Background = new SolidColorBrush(Color.FromRgb(0xEC, 0x48, 0x99)); // Pink
        }
        else if (q.StartsWith("pdf:", StringComparison.OrdinalIgnoreCase) ||
                 q.StartsWith("doc:", StringComparison.OrdinalIgnoreCase) ||
                 q.StartsWith("docs:", StringComparison.OrdinalIgnoreCase) ||
                 q.StartsWith("sheet:", StringComparison.OrdinalIgnoreCase) ||
                 q.StartsWith("slide:", StringComparison.OrdinalIgnoreCase) ||
                 q.StartsWith("img:", StringComparison.OrdinalIgnoreCase) ||
                 q.StartsWith("code:", StringComparison.OrdinalIgnoreCase) ||
                 q.StartsWith("zip:", StringComparison.OrdinalIgnoreCase) ||
                 q.StartsWith("ext:", StringComparison.OrdinalIgnoreCase))
        {
            SearchPrefixBadge.Visibility = Visibility.Visible;
            SearchPrefixIcon.Data = (Geometry)FindResource("GeoFolder");
            var colonIdx = q.IndexOf(':');
            var filterTag = q[..colonIdx].ToUpperInvariant();
            SearchPrefixText.Text = $"Bộ lọc: {filterTag}";
            SearchPrefixBadge.Background = new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7)); // Sky Blue
        }
        else
        {
            SearchPrefixBadge.Visibility = Visibility.Collapsed;
        }
    }

    private static bool IsMathExpression(string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return false;
        var mathChars = new[] { '+', '-', '*', '/', '^', '%', '=' };
        return q.Any(c => mathChars.Contains(c)) && q.Any(char.IsDigit);
    }

    #region Window Dragging & Position Management

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void OnWindowDragMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            // Do not drag if user clicked interactive buttons or inputs
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<Button>(dep) != null ||
                    FindVisualParent<TextBox>(dep) != null ||
                    FindVisualParent<ComboBox>(dep) != null ||
                    FindVisualParent<ListViewItem>(dep) != null)
                {
                    return;
                }
            }

            try
            {
                double prevLeft = this.Left;
                double prevTop = this.Top;

                DragMove();

                // Check if user actually dragged to a new position
                if (Math.Abs(this.Left - prevLeft) > 1 || Math.Abs(this.Top - prevTop) > 1)
                {
                    SaveCustomWindowPosition(this.Left, this.Top);
                }
            }
            catch (InvalidOperationException)
            {
                // Can occur if mouse is not in down state when DragMove is called
            }
        }
    }

    private async void SaveCustomWindowPosition(double left, double top)
    {
        _hasCustomPosition = true;
        _customPositionX = left;
        _customPositionY = top;

        await _settingsRepo.SetAsync("AppWindowCustomPositionEnabled", "true");
        await _settingsRepo.SetAsync("AppWindowCustomPositionX", left.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await _settingsRepo.SetAsync("AppWindowCustomPositionY", top.ToString(System.Globalization.CultureInfo.InvariantCulture));

        UpdateResetPositionButtonState();
        ShowToast($"📍 Đã lưu vị trí cửa sổ (X: {(int)left}, Y: {(int)top})");
    }

    private async void OnResetPositionClicked(object sender, RoutedEventArgs e)
    {
        await ResetPositionToCenterAsync();
    }

    public async Task ResetPositionToCenterAsync()
    {
        _hasCustomPosition = false;
        await _settingsRepo.SetAsync("AppWindowCustomPositionEnabled", "false");

        MonitorService.PositionWindow(this, _monitorTargetMode, _specificMonitorDevice, _windowPositionMode, false);
        UpdateResetPositionButtonState();
        ShowToast("🎯 Đã đưa cửa sổ về vị trí trung tâm");
    }

    public void UpdateResetPositionButtonState()
    {
        if (ResetPositionBtn == null) return;

        if (_hasCustomPosition)
        {
            ResetPositionBtn.Visibility = Visibility.Visible;
            if (CustomPosIndicator != null) CustomPosIndicator.Visibility = Visibility.Visible;
            if (ResetPosLabel != null) ResetPosLabel.Text = "Reset vị trí";
            ResetPositionBtn.ToolTip = $"Đang dùng vị trí tùy chỉnh (X: {(int)_customPositionX}, Y: {(int)_customPositionY}). Nhấp để đưa về trung tâm.";
        }
        else
        {
            ResetPositionBtn.Visibility = Visibility.Collapsed;
            if (CustomPosIndicator != null) CustomPosIndicator.Visibility = Visibility.Collapsed;
            if (ResetPosLabel != null) ResetPosLabel.Text = "Vị trí";
            ResetPositionBtn.ToolTip = "Cửa sổ đang ở vị trí trung tâm. Kéo thanh tiêu đề để di chuyển & lưu vị trí mới.";
        }
    }

    #endregion
}