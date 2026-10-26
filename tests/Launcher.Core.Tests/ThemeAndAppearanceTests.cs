using System.Windows.Media;
using Launcher.App.Services;
using Launcher.Database;
using Launcher.Database.Repositories;
using Xunit;

namespace Launcher.Core.Tests;

public class ThemeAndAppearanceTests
{
    [Fact]
    public void GetDefaultAccentHex_ReturnsExpectedHexForThemes()
    {
        Assert.Equal("#8FF0A4", ThemeManager.GetDefaultAccentHex(ThemeMode.Linux));
        Assert.Equal("#3B82F6", ThemeManager.GetDefaultAccentHex(ThemeMode.Light));
        Assert.Equal("#3B82F6", ThemeManager.GetDefaultAccentHex(ThemeMode.Glass));
        Assert.Equal("#F43F5E", ThemeManager.GetDefaultAccentHex(ThemeMode.Cyberpunk));
        Assert.Equal("#88C0D0", ThemeManager.GetDefaultAccentHex(ThemeMode.Nord));
        Assert.Equal("#BD93F9", ThemeManager.GetDefaultAccentHex(ThemeMode.Dracula));
        Assert.Equal("#7AA2F7", ThemeManager.GetDefaultAccentHex(ThemeMode.TokyoNight));
        Assert.Equal("#10B981", ThemeManager.GetDefaultAccentHex(ThemeMode.Emerald));
        Assert.Equal("#FB923C", ThemeManager.GetDefaultAccentHex(ThemeMode.Sunset));
        Assert.Equal("#E95420", ThemeManager.GetDefaultAccentHex(ThemeMode.Ubuntu));
        Assert.Equal("#00AAD4", ThemeManager.GetDefaultAccentHex(ThemeMode.Arch));
        Assert.Equal("#8B5CF6", ThemeManager.GetDefaultAccentHex(ThemeMode.Aurora));
        Assert.Equal("#3B82F6", ThemeManager.GetDefaultAccentHex(ThemeMode.Dark));
        Assert.Equal("#3B82F6", ThemeManager.GetDefaultAccentHex(ThemeMode.System));
    }

    [Fact]
    public void GetWindowsAccentColorHex_ReturnsValidHex()
    {
        var hex = ThemeManager.GetWindowsAccentColorHex();
        Assert.NotNull(hex);
        Assert.StartsWith("#", hex);
        Assert.Equal(7, hex.Length); // #RRGGBB
        var color = ThemeManager.ColorFromHex(hex);
        Assert.NotEqual(default, color);
    }

    [Theory]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("00FF00", 0, 255, 0)]
    [InlineData("#0000FF", 0, 0, 255)]
    [InlineData("#121316", 18, 19, 22)]
    public void ColorFromHex_ParsesColorsCorrectly(string hex, byte r, byte g, byte b)
    {
        var color = ThemeManager.ColorFromHex(hex);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Fact]
    public void LoadConfiguration_SetsPropertiesCorrectly()
    {
        ThemeManager.LoadConfiguration(
            theme: ThemeMode.Nord,
            accentHex: "#10B981",
            useWindowsAccent: false,
            accentOnBorder: true,
            glassEnabled: true,
            glassTintHex: "#1E293B",
            glassOpacity: 0.75,
            glassTintWithAccent: false
        );

        Assert.Equal(ThemeMode.Nord, ThemeManager.CurrentTheme);
        Assert.Equal("#10B981", ThemeManager.CustomAccentColorHex);
        Assert.False(ThemeManager.UseWindowsAccentColor);
        Assert.True(ThemeManager.ApplyAccentToBorder);
        Assert.True(ThemeManager.IsGlassEffectEnabled);
        Assert.Equal("#1E293B", ThemeManager.CustomGlassTintHex);
        Assert.Equal(0.75, ThemeManager.GlassOpacity, 2);
        Assert.False(ThemeManager.GlassTintWithAccent);

        Assert.Equal("#10B981", ThemeManager.ActiveAccentHex);
        Assert.Equal("#1E293B", ThemeManager.ActiveGlassTintHex);
    }

    [Fact]
    public void ActiveAccentHex_WhenUseWindowsAccent_ReturnsWindowsColor()
    {
        ThemeManager.SetUseWindowsAccentColor(true);
        var expected = ThemeManager.GetWindowsAccentColorHex();
        Assert.Equal(expected, ThemeManager.ActiveAccentHex);
    }

    [Fact]
    public void ActiveGlassTintHex_WhenGlassTintWithAccent_ReturnsActiveAccent()
    {
        ThemeManager.LoadConfiguration(
            theme: ThemeMode.Dark,
            accentHex: "#F43F5E",
            useWindowsAccent: false,
            accentOnBorder: false,
            glassEnabled: true,
            glassTintHex: null,
            glassOpacity: 0.85,
            glassTintWithAccent: true
        );

        Assert.Equal("#F43F5E", ThemeManager.ActiveAccentHex);
        Assert.Equal("#F43F5E", ThemeManager.ActiveGlassTintHex);
    }

    [Fact]
    public async Task SettingsRepository_StoresAndRetrieves_AllAppearanceKeys()
    {
        var testDbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lailauncher_appr_test_{Guid.NewGuid():N}.db");
        var connFactory = new SqliteConnectionFactory(testDbPath);
        var dbInit = new DatabaseInitializer(connFactory);
        dbInit.Initialize();

        try
        {
            var repo = new SettingsRepository(connFactory);

            await repo.SetAsync("AppTheme", "System");
            await repo.SetAsync("AppAccentColor", "#0EA5E9");
            await repo.SetAsync("AppAccentFromWindows", "True");
            await repo.SetAsync("AppAccentOnBorder", "True");
            await repo.SetAsync("AppGlassEnabled", "True");
            await repo.SetAsync("AppGlassTintColor", "#0E1015");
            await repo.SetAsync("AppGlassOpacity", "0.80");
            await repo.SetAsync("AppGlassTintWithAccent", "True");

            Assert.Equal("System", await repo.GetAsync("AppTheme"));
            Assert.Equal("#0EA5E9", await repo.GetAsync("AppAccentColor"));
            Assert.Equal("True", await repo.GetAsync("AppAccentFromWindows"));
            Assert.Equal("True", await repo.GetAsync("AppAccentOnBorder"));
            Assert.Equal("True", await repo.GetAsync("AppGlassEnabled"));
            Assert.Equal("#0E1015", await repo.GetAsync("AppGlassTintColor"));
            Assert.Equal("0.80", await repo.GetAsync("AppGlassOpacity"));
            Assert.Equal("True", await repo.GetAsync("AppGlassTintWithAccent"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (System.IO.File.Exists(testDbPath))
            {
                try { System.IO.File.Delete(testDbPath); } catch { }
            }
        }
    }
}
