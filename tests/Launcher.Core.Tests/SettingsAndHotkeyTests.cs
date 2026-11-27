using System.IO;
using Path = System.IO.Path;
using System.Windows.Input;
using Launcher.App.Services;
using Launcher.Database;
using Launcher.Database.Repositories;
using Xunit;

namespace Launcher.Core.Tests;

[Collection("WpfApplication")]
public class SettingsAndHotkeyTests
{
    [Theory]
    [InlineData("Alt + Space", ModifierKeys.Alt, Key.Space)]
    [InlineData("Ctrl+Space", ModifierKeys.Control, Key.Space)]
    [InlineData("Win + Space", ModifierKeys.Windows, Key.Space)]
    [InlineData("Ctrl + Shift + Space", ModifierKeys.Control | ModifierKeys.Shift, Key.Space)]
    [InlineData("Alt + K", ModifierKeys.Alt, Key.K)]
    public void TryParseHotkey_ValidStrings_ParsesCorrectly(string input, ModifierKeys expectedMods, Key expectedKey)
    {
        var success = GlobalHotkey.TryParseHotkey(input, out var mods, out var key);

        Assert.True(success);
        Assert.Equal(expectedMods, mods);
        Assert.Equal(expectedKey, key);
    }

    [Fact]
    public void FormatHotkey_ProducesExpectedString()
    {
        var formatted = GlobalHotkey.FormatHotkey(ModifierKeys.Control | ModifierKeys.Shift, Key.Space);
        Assert.Equal("Ctrl + Shift + Space", formatted);

        var formattedAlt = GlobalHotkey.FormatHotkey(ModifierKeys.Alt, Key.K);
        Assert.Equal("Alt + K", formattedAlt);
    }

    [Fact]
    public void TryParseHotkey_InvalidString_ReturnsFalse()
    {
        var success = GlobalHotkey.TryParseHotkey("InvalidKey", out _, out _);
        Assert.False(success);
    }

    [Fact]
    public async Task SettingsRepository_SetAndGet_PersistsValues()
    {
        var testDbPath = Path.Combine(Path.GetTempPath(), $"lailauncher_settings_test_{Guid.NewGuid():N}.db");
        var connFactory = new SqliteConnectionFactory(testDbPath);
        var dbInit = new DatabaseInitializer(connFactory);
        dbInit.Initialize();

        try
        {
            var settingsRepo = new SettingsRepository(connFactory);

            await settingsRepo.SetAsync("AppTheme", "Glass");
            await settingsRepo.SetAsync("AppHotkey", "Ctrl + Space");
            await settingsRepo.SetAsync("AutoStartWithWindows", "True");

            var theme = await settingsRepo.GetAsync("AppTheme");
            var hotkey = await settingsRepo.GetAsync("AppHotkey");
            var autoStart = await settingsRepo.GetAsync("AutoStartWithWindows");

            Assert.Equal("Glass", theme);
            Assert.Equal("Ctrl + Space", hotkey);
            Assert.Equal("True", autoStart);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(testDbPath))
            {
                try
                {
                    File.Delete(testDbPath);
                }
                catch
                {
                    // Ignore transient lock during test cleanup
                }
            }
        }
    }

    [Fact]
    public void StartupService_IsStartupEnabled_DoesNotThrow()
    {
        var ex = Record.Exception(() => StartupService.IsStartupEnabled());
        Assert.Null(ex);
    }

    private static double RelativeLuminance(System.Windows.Media.Color c)
        => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    [Fact]
    public void ThemeManager_IconBrush_IsDarkOnLight_AndLightOnDark()
    {
        var app = WpfTestAppHelper.EnsureApplication();

        ThemeManager.ApplyTheme(ThemeMode.Light);
        var lightBrush = app.Resources["IconBrush"] as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(lightBrush);
        Assert.True(RelativeLuminance(lightBrush.Color) < 0.45, $"Expected dark ink IconBrush in Light mode, got {lightBrush.Color}");

        ThemeManager.ApplyThemePreset(ThemeMode.Cyberpunk);
        var cyberLightBrush = app.Resources["IconBrush"] as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(cyberLightBrush);

        ThemeManager.ApplyThemePreset(ThemeMode.Emerald);
        var emeraldLightBrush = app.Resources["IconBrush"] as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(emeraldLightBrush);
        Assert.NotEqual(cyberLightBrush.Color, emeraldLightBrush.Color);

        ThemeManager.ApplyTheme(ThemeMode.Dark);
        var darkBrush = app.Resources["IconBrush"] as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(darkBrush);
        Assert.True(RelativeLuminance(darkBrush.Color) > 0.72, $"Expected bright IconBrush in Dark mode, got {darkBrush.Color}");

        ThemeManager.ApplyTheme(ThemeMode.Nord);
        var nordBrush = app.Resources["IconBrush"] as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(nordBrush);
        Assert.True(RelativeLuminance(nordBrush.Color) > 0.72, $"Expected bright IconBrush in Nord Dark mode, got {nordBrush.Color}");

        var textPrimary = app.Resources["TextPrimaryBrush"] as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(textPrimary);
        Assert.NotEqual(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF), textPrimary.Color);
    }

    [Fact]
    public void ThemeManager_WindowsPresetsAndVistaAero_ApplyExpectedResources()
    {
        var app = WpfTestAppHelper.EnsureApplication();

        ThemeManager.ApplyTheme(ThemeMode.Windows11);
        Assert.Equal(ThemeMode.Windows11, ThemeManager.CurrentTheme);
        Assert.NotNull(app.Resources["WindowBgBrush"]);

        ThemeManager.ApplyTheme(ThemeMode.Windows10);
        Assert.Equal(ThemeMode.Windows10, ThemeManager.CurrentTheme);

        ThemeManager.ApplyTheme(ThemeMode.WindowsVista);
        ThemeManager.SetLightMode(true);
        Assert.True(app.Resources["WindowBgBrush"] is System.Windows.Media.LinearGradientBrush);
    }

    [Fact]
    public void LocalizationService_SwitchesBetweenVietnameseAndEnglish()
    {
        LocalizationService.SetLanguage(LocalizationService.English);
        Assert.Equal(LocalizationService.English, LocalizationService.CurrentLanguage);
        Assert.Equal("Ready", LocalizationService.Get("Str_StatusReady"));

        LocalizationService.SetLanguage(LocalizationService.Vietnamese);
        Assert.Equal(LocalizationService.Vietnamese, LocalizationService.CurrentLanguage);
        Assert.Equal("Sẵn sàng", LocalizationService.Get("Str_StatusReady"));
    }

    [Fact]
    public void GlobalHotkey_InitialState_DefaultsToAltSpace_AndUnregistered()
    {
        using var hotkey = new GlobalHotkey();
        Assert.Equal(ModifierKeys.Alt, hotkey.Modifiers);
        Assert.Equal(Key.Space, hotkey.Key);
        Assert.False(hotkey.IsRegistered);
        Assert.Equal(HotkeyRegistrationType.None, hotkey.RegistrationType);
    }

    [Theory]
    [InlineData("alt + space", ModifierKeys.Alt, Key.Space)]
    [InlineData("ALT+SPACE", ModifierKeys.Alt, Key.Space)]
    [InlineData("  Alt   +   Space  ", ModifierKeys.Alt, Key.Space)]
    [InlineData("ctrl + space", ModifierKeys.Control, Key.Space)]
    [InlineData("win + space", ModifierKeys.Windows, Key.Space)]
    public void TryParseHotkey_CaseAndWhitespaceInsensitive_ParsesCorrectly(string input, ModifierKeys expectedMods, Key expectedKey)
    {
        var success = GlobalHotkey.TryParseHotkey(input, out var mods, out var key);
        Assert.True(success);
        Assert.Equal(expectedMods, mods);
        Assert.Equal(expectedKey, key);
    }
}
