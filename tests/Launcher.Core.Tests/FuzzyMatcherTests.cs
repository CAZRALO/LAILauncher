using Launcher.Core.Search;
using Launcher.Core.Plugins;
using Xunit;

namespace Launcher.Core.Tests;

public class FuzzyMatcherTests
{
    [Fact]
    public void Score_ExactMatch_Returns100()
    {
        var score = FuzzyMatcher.Score("Chrome", "Chrome");
        Assert.Equal(100.0, score);
    }

    [Fact]
    public void Score_PrefixMatch_ReturnsHighScore()
    {
        var score = FuzzyMatcher.Score("Visual Studio Code", "Visual");
        Assert.True(score >= 90.0);
    }

    [Fact]
    public void Score_WordPrefixMatch_ReturnsHighScore()
    {
        var score = FuzzyMatcher.Score("Visual Studio Code", "Studio");
        Assert.True(score >= 80.0);
    }

    [Fact]
    public void Score_AcronymMatch_ReturnsScore()
    {
        var score = FuzzyMatcher.Score("Visual Studio", "vs");
        Assert.True(score >= 70.0);
    }

    [Fact]
    public void Score_WordExactMatch_GoogleGemini()
    {
        var score = FuzzyMatcher.Score("Google Gemini", "gemini");
        Assert.True(score >= 95.0, $"Expected >= 95.0, but got {score}");
    }

    [Theory]
    [InlineData("Shut Down", "shut")]
    [InlineData("Sleep", "slee")]
    [InlineData("Hibernate", "hiber")]
    [InlineData("Restart Computer", "resta")]
    [InlineData("Lock Screen", "lock")]
    public void Score_PrefixMatch_OnSystemCommands(string target, string query)
    {
        var score = FuzzyMatcher.Score(target, query);
        Assert.True(score >= 90.0, $"Query '{query}' on '{target}' scored {score}, expected >= 90.0");
    }

    [Theory]
    [InlineData("Tắt máy", "tat may")]
    [InlineData("Khởi động lại", "khoi dong")]
    [InlineData("Cài đặt hệ thống", "cai dat")]
    [InlineData("Khóa máy tính", "khoa")]
    [InlineData("Ngủ đông", "ngu dong")]
    public void Score_VietnameseUnaccent_MatchesAccentedTarget(string target, string query)
    {
        var score = FuzzyMatcher.Score(target, query);
        Assert.True(score >= 85.0, $"Query '{query}' on '{target}' scored {score}, expected >= 85.0");
    }

    [Fact]
    public void Score_MultiToken_OrderIndependent()
    {
        var score = FuzzyMatcher.Score("Google Gemini", "gemini google");
        Assert.True(score >= 90.0, $"Expected >= 90.0, but got {score}");
    }

    [Theory]
    [InlineData("CPU-Info", "cpu info")]
    [InlineData("CPU-Info", "cpuinfo")]
    [InlineData("CPU-Info", "cpu")]
    [InlineData("CPU-Z", "cpuz")]
    [InlineData("CPU-Z", "cpu-z")]
    [InlineData("Scrble Ink", "scrble ink")]
    [InlineData("Scrble Ink", "scrbleink")]
    [InlineData("Scrble Ink", "scrble")]
    [InlineData("Notepad", "notepad")]
    [InlineData("Notepad", "notepd")]
    [InlineData("Arc", "arc")]
    [InlineData("Arc", "arc browser")]
    public void Score_AppMatching_HighScores(string target, string query)
    {
        var score = FuzzyMatcher.Score(target, query);
        Assert.True(score >= 35.0, $"Query '{query}' on '{target}' scored {score}, expected >= 35.0");
    }

    [Fact]
    public void Score_OverloadWithPrecomputedDiacritics_ProducesIdenticalScore()
    {
        var target = "Báo cáo tài chính 2026.xlsx";
        var query = "tai chinh";
        var unaccentQuery = Launcher.Core.Utilities.VietnameseTextHelper.RemoveDiacritics(query);
        var unaccentTarget = Launcher.Core.Utilities.VietnameseTextHelper.RemoveDiacritics(target);

        var scoreDefault = FuzzyMatcher.Score(target, query);
        var scoreOverload = FuzzyMatcher.Score(target, query, unaccentQuery, unaccentTarget);

        Assert.Equal(scoreDefault, scoreOverload, 4);
        Assert.True(scoreOverload >= 70.0);
    }
}
