using Launcher.Core.Plugins;
using Launcher.SDK;
using Xunit;

namespace Launcher.Core.Tests;

public class PluginsTests
{
    [Fact]
    public async Task SystemCommandsPlugin_KeywordQuery_ReturnsAllCommands()
    {
        var plugin = new SystemCommandsPlugin();
        var context = new QueryContext("sys");
        var results = (await plugin.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title.Contains("Lock", StringComparison.OrdinalIgnoreCase) || r.Title.Contains("Shut", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("shut", "Shut Down")]
    [InlineData("shutdown", "Shut Down")]
    [InlineData("tat", "Shut Down")]
    [InlineData("slee", "Sleep")]
    [InlineData("hiber", "Hibernate")]
    [InlineData("rest", "Restart Computer")]
    [InlineData("lock", "Lock Screen")]
    public async Task SystemCommandsPlugin_PrefixQuery_ReturnsMatchingCommand(string query, string expectedTitle)
    {
        var plugin = new SystemCommandsPlugin();
        var context = new QueryContext(query);
        var results = (await plugin.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        var matched = results.FirstOrDefault(r => r.Title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(matched);
        Assert.NotEmpty(matched.Actions);
    }

    [Fact]
    public async Task WindowsSettingsPlugin_KeywordQuery_ReturnsSettings()
    {
        var plugin = new WindowsSettingsPlugin();
        var context = new QueryContext("set wifi");
        var results = (await plugin.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WindowsSettingsPlugin_DirectQuery_FindsBluetooth()
    {
        var plugin = new WindowsSettingsPlugin();
        var context = new QueryContext("bluetooth");
        var results = (await plugin.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WebSearchPlugin_KeywordQuery_BuildsCorrectUrl()
    {
        var plugin = new WebSearchPlugin();
        var context = new QueryContext("yt lofi hip hop");
        var results = (await plugin.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.Contains("YouTube", first.Title);
        Assert.Contains("youtube.com/results?search_query=lofi%20hip%20hop", first.Subtitle);
    }

    [Fact]
    public async Task WebSearchPlugin_GitHubSearch_BuildsCorrectUrl()
    {
        var plugin = new WebSearchPlugin();
        var context = new QueryContext("gh dotnet/runtime");
        var results = (await plugin.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.Contains("GitHub", first.Title);
        Assert.Contains("github.com/search?q=dotnet%2Fruntime", first.Subtitle);
    }

    [Fact]
    public async Task ProcessKillerPlugin_KeywordQuery_ReturnsProcesses()
    {
        var plugin = new ProcessKillerPlugin();
        var context = new QueryContext("kill");
        var results = (await plugin.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(ResultType.Process, r.Type));
    }

    [Fact]
    public async Task ProcessKillerPlugin_CriticalProcess_HasSafetyProtection()
    {
        var plugin = new ProcessKillerPlugin();
        var context = new QueryContext("kill System");
        var results = (await plugin.QueryAsync(context)).ToList();

        var sysResult = results.FirstOrDefault(r => r.Metadata.TryGetValue("ProcessName", out var name) && name.Equals("System", StringComparison.OrdinalIgnoreCase));
        if (sysResult != null)
        {
            Assert.Equal("True", sysResult.Metadata["IsCritical"]);
            Assert.Contains("Bảo vệ", sysResult.Actions[0].Name);
        }
    }

    [Theory]
    [InlineData("dark mode", "Dark / Light Mode")]
    [InlineData("am luong", "Volume")]
    [InlineData("volume", "Volume")]
    [InlineData("do sang", "Brightness")]
    [InlineData("brightness", "Brightness")]
    [InlineData("tiet kiem pin", "Battery Saver")]
    [InlineData("battery saver", "Battery Saver")]
    [InlineData("wifi", "Wi-Fi")]
    [InlineData("danh sach wifi", "Wi-Fi")]
    [InlineData("bluetooth", "Bluetooth")]
    [InlineData("hotspot", "Hotspot")]
    [InlineData("mobile hotspot", "Hotspot")]
    [InlineData("taskbar", "Taskbar")]
    [InlineData("mau theme", "Accent Color")]
    public async Task SystemCommandsPlugin_NewWindowsControls_MatchDirectQueries(string query, string expectedSnippet)
    {
        var plugin = new SystemCommandsPlugin();
        var context = new QueryContext(query);
        var results = (await plugin.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        var matched = results.FirstOrDefault(r => r.Title.Contains(expectedSnippet, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(matched);
        Assert.NotEmpty(matched.Actions);
    }

    [Fact]
    public async Task SystemCommandsPlugin_WinKeyword_ReturnsWindowsControls()
    {
        var plugin = new SystemCommandsPlugin();
        var context = new QueryContext("win");
        var results = (await plugin.QueryAsync(context)).ToList();

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Title.Contains("Dark", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.Title.Contains("Volume", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.Title.Contains("Brightness", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.Title.Contains("Battery", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.Title.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.Title.Contains("Taskbar", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("sys wifi list")]
    [InlineData("sys wifi list home")]
    [InlineData("wifi list")]
    public async Task SystemCommandsPlugin_WifiListQuery_ReturnsInAppWifiResultsOnly(string query)
    {
        var plugin = new SystemCommandsPlugin();
        var results = (await plugin.QueryAsync(new QueryContext(query))).ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(ResultType.SystemCommand, r.Type));
        Assert.Contains(results, r => r.Title.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, r => r.Title.Contains("Lock Workstation", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("sys bluetooth list")]
    [InlineData("sys bluetooth list sony")]
    [InlineData("bluetooth list")]
    public async Task SystemCommandsPlugin_BluetoothListQuery_ReturnsInAppBluetoothResultsOnly(string query)
    {
        var plugin = new SystemCommandsPlugin();
        var results = (await plugin.QueryAsync(new QueryContext(query))).ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(ResultType.SystemCommand, r.Type));
        Assert.Contains(results, r => r.Title.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(results, r => r.Title.Contains("Shut Down", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SystemCommandsPlugin_SysFilteredQuery_ExcludesUnrelatedCommands()
    {
        var plugin = new SystemCommandsPlugin();
        var results = (await plugin.QueryAsync(new QueryContext("sys battery"))).ToList();

        Assert.NotEmpty(results);
        Assert.All(results, r =>
            Assert.True(
                r.Title.Contains("Battery", StringComparison.OrdinalIgnoreCase) ||
                r.Title.Contains("pin", StringComparison.OrdinalIgnoreCase) ||
                r.Subtitle.Contains("pin", StringComparison.OrdinalIgnoreCase),
                $"Unexpected unrelated result for 'sys battery': {r.Title}"));
    }

    [Fact]
    public async Task SystemCommandsPlugin_WifiAndBluetoothShortcuts_TargetInAppListQueries()
    {
        var plugin = new SystemCommandsPlugin();

        var wifiResults = (await plugin.QueryAsync(new QueryContext("sys wifi"))).ToList();
        Assert.Contains(wifiResults, r =>
            r.Metadata.TryGetValue("ActionKeyword", out var kw) &&
            kw == "sys wifi list");

        var btResults = (await plugin.QueryAsync(new QueryContext("sys bluetooth"))).ToList();
        Assert.Contains(btResults, r =>
            r.Metadata.TryGetValue("ActionKeyword", out var kw) &&
            kw == "sys bluetooth list");
    }
}
