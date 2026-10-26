using System.Text.RegularExpressions;
using Launcher.SDK;

namespace Launcher.AI;

public class AIPlugin : ISearchProvider
{
    private readonly IAIProvider _aiProvider;
    private readonly IntentClassifier _intentClassifier;

    public string ProviderId => "ai_assistant";
    public string DisplayName => "Local AI Assistant";
    public int Priority => 5;
    public string? ActionKeyword => "ai";
    public bool SupportsGlobalSearch => true;

    // Action callback to trigger UI window opening from MainWindow
    public Action<string, string>? OnRequestOpenAiWindow { get; set; }

    // Clipboard and Notification delegates supplied by UI layer
    public Func<string?>? GetClipboardText { get; set; }
    public Action<string>? ShowNotice { get; set; }

    public AIPlugin(IAIProvider aiProvider, IntentClassifier intentClassifier)
    {
        _aiProvider = aiProvider;
        _intentClassifier = intentClassifier;
    }

    public Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context)
    {
        var rawQuery = context.RawQuery;
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return Task.FromResult<IEnumerable<QueryResult>>([]);
        }

        var trimmed = rawQuery.Trim();
        var results = new List<QueryResult>();

        // 1. Check for AI action keyword or explicit prefixes
        var isAiExplicit = trimmed.Equals("ai", StringComparison.OrdinalIgnoreCase) ||
                           trimmed.Equals("ai:", StringComparison.OrdinalIgnoreCase) ||
                           trimmed.StartsWith("ai ", StringComparison.OrdinalIgnoreCase) ||
                           trimmed.StartsWith("ai:", StringComparison.OrdinalIgnoreCase) ||
                           trimmed.StartsWith("? ", StringComparison.OrdinalIgnoreCase) ||
                           trimmed.StartsWith("hỏi ai ", StringComparison.OrdinalIgnoreCase);

        // 2. If not explicitly prefixed, test using ONNX IntentClassifier
        if (!isAiExplicit)
        {
            var intentResult = _intentClassifier.Predict(trimmed);
            if (intentResult.Intent == IntentType.AI && intentResult.Confidence >= 0.85f)
            {
                isAiExplicit = true;
            }
        }

        if (!isAiExplicit)
        {
            return Task.FromResult<IEnumerable<QueryResult>>([]);
        }

        // Handle bare "ai" or "ai:" -> Show quick action suggestions
        if (trimmed.Equals("ai", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("ai:", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new QueryResult
            {
                Id = "ai_prompt_guide",
                Title = "🤖 Trợ lý AI Cục bộ (Ollama)",
                Subtitle = $"Nhập câu hỏi sau 'ai:' hoặc 'ai ' (Model: {_aiProvider.DefaultModel})",
                Score = 1000.0,
                Type = ResultType.AI,
                IconGlyph = "🤖",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Ask AI",
                        SyncAction = () => OnRequestOpenAiWindow?.Invoke("Xin chào, bạn có thể giúp gì cho tôi?", _aiProvider.DefaultModel)
                    }
                ]
            });

            results.Add(new QueryResult
            {
                Id = "ai_clipboard_summarize",
                Title = "📋 Tóm tắt nội dung Clipboard",
                Subtitle = "ai summarize • Trích xuất ý chính từ văn bản trong khay nhớ tạm",
                Score = 990.0,
                Type = ResultType.AI,
                IconGlyph = "📝",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Tóm tắt Clipboard",
                        SyncAction = () => ExecuteClipboardAction("Hãy tóm tắt ngắn gọn, súc tích các ý chính của đoạn văn bản sau bằng tiếng Việt:")
                    }
                ]
            });

            results.Add(new QueryResult
            {
                Id = "ai_clipboard_explain",
                Title = "💡 Giải thích Code / Văn bản Clipboard",
                Subtitle = "ai explain • Phân tích và giải thích đoạn mã hoặc khái niệm trong Clipboard",
                Score = 980.0,
                Type = ResultType.AI,
                IconGlyph = "💡",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Giải thích Clipboard",
                        SyncAction = () => ExecuteClipboardAction("Hãy phân tích và giải thích chi tiết, dễ hiểu đoạn code hoặc văn bản sau:")
                    }
                ]
            });

            results.Add(new QueryResult
            {
                Id = "ai_clipboard_translate",
                Title = "🌐 Dịch Clipboard sang Tiếng Việt",
                Subtitle = "ai translate • Dịch nội dung khay nhớ tạm sang Tiếng Việt mượt mà, tự nhiên",
                Score = 970.0,
                Type = ResultType.AI,
                IconGlyph = "🌐",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Dịch Clipboard",
                        SyncAction = () => ExecuteClipboardAction("Hãy dịch đoạn văn bản sau sang Tiếng Việt một cách tự nhiên, chuẩn xác:")
                    }
                ]
            });

            results.Add(new QueryResult
            {
                Id = "ai_rag_docs",
                Title = "📄 Chat với tài liệu cục bộ (Local RAG)",
                Subtitle = "ai doc <câu hỏi> • Đặt câu hỏi trực tiếp trên tài liệu trong máy tính không qua Cloud",
                Score = 960.0,
                Type = ResultType.AI,
                IconGlyph = "📚",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Hỏi AI về tài liệu",
                        SyncAction = () => OnRequestOpenAiWindow?.Invoke("Tôi muốn tìm hiểu và tóm tắt các tài liệu trong máy tính của tôi. Hãy cho tôi biết các loại định dạng tệp bạn hỗ trợ tốt nhất.", _aiProvider.DefaultModel)
                    }
                ]
            });

            results.Add(new QueryResult
            {
                Id = "ai_semantic_guide",
                Title = "🎯 Tìm kiếm ngữ nghĩa (Zero-NPU Semantic Search)",
                Subtitle = "Gõ tự nhiên như 'báo cáo tài chính', 'hợp đồng', 'ảnh hoàng hôn' để tìm file theo ngữ cảnh",
                Score = 950.0,
                Type = ResultType.AI,
                IconGlyph = "🎯",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Xem hướng dẫn",
                        SyncAction = () => ShowNotice?.Invoke("Mẹo: Gõ câu mô tả tự nhiên (ví dụ 'báo cáo tài chính', 'ảnh hoàng hôn') để tìm kiếm tệp theo ngữ nghĩa mà không cần NPU!")
                    }
                ]
            });

            return Task.FromResult<IEnumerable<QueryResult>>(results);
        }

        // Clean query to get prompt or specific command
        var cleanPrompt = Regex.Replace(trimmed, @"^(ai:\s*|\?\s*|hỏi ai\s*|ai\s+)", "", RegexOptions.IgnoreCase).Trim();

        // Check for clipboard sub-commands
        if (cleanPrompt.Equals("summarize", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.Equals("summary", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.Equals("tóm tắt", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.Equals("tom tat", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new QueryResult
            {
                Id = "ai_cmd_summarize",
                Title = "📝 Tóm tắt nội dung Clipboard",
                Subtitle = "Nhấn Enter để gửi nội dung trong Clipboard đến AI và nhận tóm tắt",
                Score = 1000.0,
                Type = ResultType.AI,
                IconGlyph = "📝",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Tóm tắt Clipboard",
                        SyncAction = () => ExecuteClipboardAction("Hãy tóm tắt ngắn gọn, súc tích các ý chính của đoạn văn bản sau bằng tiếng Việt:")
                    }
                ]
            });
            return Task.FromResult<IEnumerable<QueryResult>>(results);
        }

        if (cleanPrompt.Equals("explain", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.Equals("giải thích", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.Equals("giai thich", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new QueryResult
            {
                Id = "ai_cmd_explain",
                Title = "💡 Giải thích Code / Văn bản Clipboard",
                Subtitle = "Nhấn Enter để gửi nội dung trong Clipboard đến AI và nhận giải thích",
                Score = 1000.0,
                Type = ResultType.AI,
                IconGlyph = "💡",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Giải thích Clipboard",
                        SyncAction = () => ExecuteClipboardAction("Hãy phân tích và giải thích chi tiết, dễ hiểu đoạn code hoặc văn bản sau:")
                    }
                ]
            });
            return Task.FromResult<IEnumerable<QueryResult>>(results);
        }

        if (cleanPrompt.Equals("translate", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.Equals("dịch", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.Equals("dich", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new QueryResult
            {
                Id = "ai_cmd_translate",
                Title = "🌐 Dịch Clipboard sang Tiếng Việt",
                Subtitle = "Nhấn Enter để gửi nội dung trong Clipboard đến AI và nhận bản dịch",
                Score = 1000.0,
                Type = ResultType.AI,
                IconGlyph = "🌐",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Dịch Clipboard",
                        SyncAction = () => ExecuteClipboardAction("Hãy dịch đoạn văn bản sau sang Tiếng Việt một cách tự nhiên, chuẩn xác:")
                    }
                ]
            });
            return Task.FromResult<IEnumerable<QueryResult>>(results);
        }

        // Check for Document / Local RAG query
        if (cleanPrompt.StartsWith("doc ", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.StartsWith("doc:", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.StartsWith("file ", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.Equals("doc", StringComparison.OrdinalIgnoreCase) ||
            cleanPrompt.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            var docQuery = Regex.Replace(cleanPrompt, @"^(doc:|doc\s+|file:|file\s+)", "", RegexOptions.IgnoreCase).Trim();
            results.Add(new QueryResult
            {
                Id = "ai_doc_rag_action",
                Title = string.IsNullOrEmpty(docQuery) ? "📄 Chat với tài liệu (Local RAG)" : $"Hỏi về tài liệu: \"{docQuery}\"",
                Subtitle = "Nhấn Enter để gửi truy vấn đến Ollama với ngữ cảnh tài liệu",
                Score = 1000.0,
                Type = ResultType.AI,
                IconGlyph = "📚",
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Chat với tài liệu",
                        SyncAction = () => OnRequestOpenAiWindow?.Invoke(
                            string.IsNullOrEmpty(docQuery) 
                                ? "Hãy hướng dẫn tôi cách sử dụng Local AI Launcher để tìm kiếm ngữ nghĩa và chat với tài liệu." 
                                : $"Tôi muốn phân tích và tóm tắt tài liệu liên quan đến: {docQuery}",
                            _aiProvider.DefaultModel)
                    }
                ]
            });
            return Task.FromResult<IEnumerable<QueryResult>>(results);
        }

        // Generic query prompt for AI
        if (!string.IsNullOrWhiteSpace(cleanPrompt))
        {
            results.Add(new QueryResult
            {
                Id = $"ai_query_{cleanPrompt.GetHashCode()}",
                Title = $"Hỏi AI: \"{cleanPrompt}\"",
                Subtitle = $"Nhấn Enter để stream câu trả lời từ Ollama ({_aiProvider.DefaultModel})",
                Score = 990.0,
                Type = ResultType.AI,
                IconGlyph = "🤖",
                Metadata = new Dictionary<string, string>
                {
                    ["Prompt"] = cleanPrompt,
                    ["Model"] = _aiProvider.DefaultModel
                },
                Actions =
                [
                    new ResultAction
                    {
                        Name = "Ask AI",
                        Description = "Gửi câu hỏi và xem phản hồi thời gian thực",
                        IconGlyph = "🚀",
                        ShortcutHint = "Enter",
                        SyncAction = () => OnRequestOpenAiWindow?.Invoke(cleanPrompt, _aiProvider.DefaultModel)
                    }
                ]
            });
        }

        return Task.FromResult<IEnumerable<QueryResult>>(results);
    }

    private void ExecuteClipboardAction(string instructionPrefix)
    {
        try
        {
            var text = GetClipboardText?.Invoke();

            if (string.IsNullOrWhiteSpace(text))
            {
                ShowNotice?.Invoke("Khay nhớ tạm (Clipboard) hiện không có văn bản nào!");
                return;
            }

            var fullPrompt = $"{instructionPrefix}\n\n\"\"\"\n{text.Trim()}\n\"\"\"";
            OnRequestOpenAiWindow?.Invoke(fullPrompt, _aiProvider.DefaultModel);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AIPlugin] Clipboard action error: {ex.Message}");
        }
    }
}
