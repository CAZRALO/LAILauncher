using System.Text.RegularExpressions;
using Launcher.SDK;

namespace Launcher.Core.Search;

/// <summary>
/// Lightweight, zero-NPU Local Semantic Matcher.
/// Understands natural language concepts, intent domains, and semantic synonyms for files, system controls, and tasks.
/// </summary>
public static class SemanticMatcher
{
    private static readonly Dictionary<string, (string[] Keywords, string[] Extensions, string[] SystemActions)> SemanticTaxonomy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Financial & Reports"] = (
            Keywords: ["bao cao", "báo cáo", "tai chinh", "tài chính", "doanh thu", "loi nhuan", "lợi nhuận", "chi tieu", "chi tiêu", "ngan sach", "ngân sách", "budget", "finance", "financial", "report", "revenue", "invoice", "hoa don", "hóa đơn", "tax", "thue", "thuế", "luong", "lương"],
            Extensions: [".xlsx", ".xls", ".csv", ".pdf", ".docx"],
            SystemActions: []
        ),
        ["Contracts & Legal"] = (
            Keywords: ["hop dong", "hợp đồng", "thoa thuan", "thỏa thuận", "ky ket", "ký kết", "contract", "agreement", "nda", "legal", "phap ly", "pháp lý", "dieu khoan", "điều khoản", "bien ban", "biên bản"],
            Extensions: [".docx", ".doc", ".pdf"],
            SystemActions: []
        ),
        ["Plans & Presentations"] = (
            Keywords: ["ke hoach", "kế hoạch", "chien luoc", "chiến lược", "thuyet trinh", "thuyết trình", "slide", "presentation", "pitch", "plan", "roadmap", "proposal", "de xuat", "đề xuất"],
            Extensions: [".pptx", ".ppt", ".pdf", ".key"],
            SystemActions: []
        ),
        ["Academic & Books"] = (
            Keywords: ["luan van", "luận văn", "do an", "đồ án", "khoa hoc", "khoa học", "giao trinh", "giáo trình", "tai lieu", "tài liệu", "thesis", "research", "paper", "book", "sach", "sách", "tutorial", "huong dan", "hướng dẫn", "study"],
            Extensions: [".pdf", ".epub", ".docx", ".md"],
            SystemActions: []
        ),
        ["Photos & Graphics"] = (
            Keywords: ["anh", "ảnh", "hinh", "hình", "photo", "picture", "image", "wallpaper", "hinh nen", "hình nền", "screenshot", "chup man hinh", "chụp màn hình", "avatar", "camera", "sunset", "hoang hon", "hoàng hôn", "bien", "biển", "beach"],
            Extensions: [".png", ".jpg", ".jpeg", ".webp", ".svg", ".gif", ".bmp", ".ico"],
            SystemActions: []
        ),
        ["Audio & Music"] = (
            Keywords: ["nhac", "nhạc", "bai hat", "bài hát", "audio", "music", "song", "sound", "am thanh", "âm thanh", "podcast", "album", "mp3"],
            Extensions: [".mp3", ".wav", ".flac", ".m4a", ".ogg"],
            SystemActions: []
        ),
        ["Video & Movies"] = (
            Keywords: ["video", "clip", "phim", "movie", "film", "ghi hinh", "ghi hình", "record", "recording", "mp4"],
            Extensions: [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm"],
            SystemActions: []
        ),
        ["Source Code & Dev"] = (
            Keywords: ["code", "ma nguon", "mã nguồn", "source", "du an", "dự án", "project", "repo", "repository", "solution", "backend", "frontend", "api", "script", "program"],
            Extensions: [".cs", ".py", ".js", ".ts", ".html", ".css", ".json", ".xml", ".sql", ".cpp", ".c", ".h", ".sln", ".csproj"],
            SystemActions: []
        ),
        ["Compressed Archives"] = (
            Keywords: ["nen", "nén", "giai nen", "giải nén", "archive", "zip", "rar", "backup", "sao luu", "sao lưu", "package"],
            Extensions: [".zip", ".rar", ".7z", ".tar", ".gz"],
            SystemActions: []
        ),
        ["Battery & Power"] = (
            Keywords: ["pin", "tiet kiem pin", "tiết kiệm pin", "battery", "battery saver", "power saver", "sac", "sạc", "nang luong", "năng lượng"],
            Extensions: [],
            SystemActions: ["Enable Battery Saver (Power Saver)", "Balanced Power Mode", "Battery & Power Settings"]
        ),
        ["Volume & Audio Control"] = (
            Keywords: ["am luong", "âm lượng", "loa", "volume", "sound", "tieng", "tiếng", "nho loa", "to loa", "mute", "tat am", "tắt âm", "bat am", "bật âm", "tai nghe", "headphone"],
            Extensions: [],
            SystemActions: ["Volume Up (+5%)", "Volume Down (-5%)", "Toggle Mute / Unmute", "Volume Mixer"]
        ),
        ["Display & Brightness"] = (
            Keywords: ["do sang", "độ sáng", "toi man hinh", "tối màn hình", "sang man hinh", "sáng màn hình", "brightness", "dim", "man hinh", "màn hình", "night light", "anh sang vang", "ánh sáng vàng"],
            Extensions: [],
            SystemActions: ["Brightness Up (+10%)", "Brightness Down (-10%)", "Set Brightness 100%", "Set Brightness 50%"]
        ),
        ["Network & Connectivity"] = (
            Keywords: ["wifi", "wi-fi", "mang", "mạng", "ket noi", "kết nối", "internet", "hotspot", "phat wifi", "phát wifi", "bluetooth", "chia se mang", "chia sẻ mạng"],
            Extensions: [],
            SystemActions: ["Show Available Wi-Fi Networks", "Toggle Mobile Hotspot", "Bluetooth & Devices Settings"]
        ),
        ["Dark & Light Theme"] = (
            Keywords: ["dark mode", "light mode", "che do toi", "chế độ tối", "che do sang", "chế độ sáng", "giao dien", "giao diện", "mau nen", "màu nền", "black theme"],
            Extensions: [],
            SystemActions: ["Toggle Windows Dark / Light Mode", "Windows Dark Mode", "Windows Light Mode"]
        )
    };

    /// <summary>
    /// Analyzes a natural query and returns matched semantic categories, target extensions, and confidence score.
    /// </summary>
    public static SemanticAnalysisResult AnalyzeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SemanticAnalysisResult();
        }

        var normalized = NormalizeText(query);
        var matchedCategories = new List<string>();
        var targetExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestedActions = new List<string>();
        double maxScore = 0.0;

        foreach (var (category, (keywords, extensions, actions)) in SemanticTaxonomy)
        {
            double catScore = 0.0;
            foreach (var kw in keywords)
            {
                var normKw = NormalizeText(kw);
                if (normalized.Contains(normKw))
                {
                    catScore += 35.0;
                }
                else
                {
                    // Word boundary partial match
                    var words = normKw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int matchedWords = words.Count(w => normalized.Contains(w));
                    if (matchedWords > 0 && words.Length > 1)
                    {
                        catScore += (double)matchedWords / words.Length * 25.0;
                    }
                }
            }

            if (catScore > 20.0)
            {
                matchedCategories.Add(category);
                foreach (var ext in extensions) targetExtensions.Add(ext);
                foreach (var act in actions) suggestedActions.Add(act);
                if (catScore > maxScore) maxScore = catScore;
            }
        }

        return new SemanticAnalysisResult
        {
            HasSemanticIntent = matchedCategories.Count > 0,
            Categories = matchedCategories,
            TargetExtensions = [.. targetExtensions],
            SuggestedActions = suggestedActions.Distinct().ToList(),
            ConfidenceScore = Math.Min(95.0, maxScore)
        };
    }

    /// <summary>
    /// Computes a semantic bonus score for a candidate file given the analyzed query intent.
    /// </summary>
    public static double EvaluateFileSemanticScore(string fileName, string? extension, string fullPath, SemanticAnalysisResult semanticIntent)
    {
        if (!semanticIntent.HasSemanticIntent)
            return 0.0;

        double bonus = 0.0;
        var normName = NormalizeText(fileName);
        var normPath = NormalizeText(fullPath);

        // 1. Extension match bonus
        if (!string.IsNullOrEmpty(extension) && semanticIntent.TargetExtensions.Contains(extension))
        {
            bonus += 25.0;
        }

        // 2. Category keyword match inside file name or path
        foreach (var cat in semanticIntent.Categories)
        {
            if (SemanticTaxonomy.TryGetValue(cat, out var entry))
            {
                foreach (var kw in entry.Keywords)
                {
                    var normKw = NormalizeText(kw);
                    if (normName.Contains(normKw))
                    {
                        bonus += 40.0;
                        break;
                    }
                    else if (normPath.Contains(normKw))
                    {
                        bonus += 15.0;
                    }
                }
            }
        }

        return Math.Min(80.0, bonus);
    }

    private static string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.ToLowerInvariant();
        
        // Remove common diacritics for flexible Vietnamese searching
        s = Regex.Replace(s, @"[àáạảãâầấậẩẫăằắặẳẵ]", "a");
        s = Regex.Replace(s, @"[èéẹẻẽêềếệểễ]", "e");
        s = Regex.Replace(s, @"[ìíịỉĩ]", "i");
        s = Regex.Replace(s, @"[òóọỏõôồốộổỗơờớợởỡ]", "o");
        s = Regex.Replace(s, @"[ùúụủũưừứựửữ]", "u");
        s = Regex.Replace(s, @"[ỳýỵỷỹ]", "y");
        s = Regex.Replace(s, @"[đ]", "d");
        
        return s;
    }
}

public class SemanticAnalysisResult
{
    public bool HasSemanticIntent { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<string> TargetExtensions { get; set; } = [];
    public List<string> SuggestedActions { get; set; } = [];
    public double ConfidenceScore { get; set; }
}
