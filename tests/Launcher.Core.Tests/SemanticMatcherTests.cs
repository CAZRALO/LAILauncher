using Launcher.Core.Search;
using Xunit;

namespace Launcher.Core.Tests;

public class SemanticMatcherTests
{
    [Theory]
    [InlineData("báo cáo tài chính quý 3", "Financial & Reports")]
    [InlineData("hop dong thue nha", "Contracts & Legal")]
    [InlineData("ke hoach chien luoc 2025", "Plans & Presentations")]
    [InlineData("luan van tot nghiep ai", "Academic & Books")]
    [InlineData("ảnh hoàng hôn bờ biển", "Photos & Graphics")]
    [InlineData("bài hát yêu thích", "Audio & Music")]
    [InlineData("video review laptop", "Video & Movies")]
    [InlineData("source code backend", "Source Code & Dev")]
    [InlineData("tiết kiệm pin", "Battery & Power")]
    [InlineData("chỉnh âm lượng loa", "Volume & Audio Control")]
    public void AnalyzeQuery_DetectsExpectedSemanticIntent(string query, string expectedCategory)
    {
        var result = SemanticMatcher.AnalyzeQuery(query);

        Assert.True(result.HasSemanticIntent);
        Assert.Contains(expectedCategory, result.Categories);
        Assert.True(result.ConfidenceScore > 0);
    }

    [Fact]
    public void EvaluateFileSemanticScore_BoostsRelevantDocument()
    {
        var intent = SemanticMatcher.AnalyzeQuery("báo cáo tài chính quý 3");
        
        var scoreExcel = SemanticMatcher.EvaluateFileSemanticScore(
            "Bao_cao_tai_chinh_Q3_2024.xlsx", 
            ".xlsx", 
            @"D:\Documents\Finance\Bao_cao_tai_chinh_Q3_2024.xlsx", 
            intent
        );

        var scoreUnrelated = SemanticMatcher.EvaluateFileSemanticScore(
            "GameInstaller.exe", 
            ".exe", 
            @"C:\Games\GameInstaller.exe", 
            intent
        );

        Assert.True(scoreExcel > 40.0);
        Assert.Equal(0.0, scoreUnrelated);
    }
}
