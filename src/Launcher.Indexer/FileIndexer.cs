using System.Diagnostics;
using System.IO;
using Launcher.SDK;
using Launcher.Core.Search;
using Launcher.Core.Actions;
using Launcher.Database.Models;
using Launcher.Database.Repositories;

namespace Launcher.Indexer;

public class FileIndexer : ISearchProvider
{
    private readonly IFileRepository _fileRepository;
    private readonly List<FileEntity> _cachedFiles = [];
    private readonly object _lock = new();
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".ps1", ".msi", ".msc", ".vbs"
    };

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".vs", ".idea", "bin", "obj", "dist", "build", "__pycache__", ".nuget", ".gradle", "packages"
    };

    public string ProviderId => "core.file_indexer";
    public string DisplayName => "File System";
    public int Priority => 2;

    public Action<string, string?>? OnRequestOpenAiWithFile { get; set; }

    public FileIndexer(IFileRepository fileRepository)
    {
        _fileRepository = fileRepository;
    }

    public async Task InitializeAsync()
    {
        // 1. Load cached files from database
        var existing = await _fileRepository.GetRecentAsync(5000);
        lock (_lock)
        {
            _cachedFiles.Clear();
            _cachedFiles.AddRange(existing);
        }

        // 2. Scan standard user directories in background
        _ = Task.Run(async () =>
        {
            try
            {
                await ScanUserDirectoriesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning file system: {ex.Message}");
            }
        });
    }

    public async Task ScanUserDirectoriesAsync()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var foldersToScan = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(userProfile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Path.Combine(userProfile, "source", "repos"),
            Path.Combine(userProfile, "Projects"),
            Path.Combine(userProfile, "Developer"),
            Path.Combine(userProfile, "workspace")
        };

        var scanned = new List<FileEntity>();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var folder in foldersToScan)
        {
            if (!Directory.Exists(folder))
                continue;

            // Index root special directory itself
            try
            {
                var dirInfo = new DirectoryInfo(folder);
                scanned.Add(new FileEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = dirInfo.Name,
                    Path = dirInfo.FullName,
                    Extension = null,
                    SizeBytes = 0,
                    IsDirectory = true,
                    ModifiedTime = new DateTimeOffset(dirInfo.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
                    IndexedAt = now
                });
            }
            catch
            {
                // Ignore
            }

            ScanDirectoryRecursively(folder, scanned, currentDepth: 0, maxDepth: 2, maxFiles: 3000, now: now);
        }

        if (scanned.Count > 0)
        {
            await _fileRepository.UpsertBatchAsync(scanned);
            var updated = await _fileRepository.GetAllAsync();
            lock (_lock)
            {
                _cachedFiles.Clear();
                _cachedFiles.AddRange(updated);
            }
        }
    }

    private void ScanDirectoryRecursively(string dirPath, List<FileEntity> results, int currentDepth, int maxDepth, int maxFiles, long now)
    {
        if (currentDepth > maxDepth || results.Count >= maxFiles)
            return;

        try
        {
            var dirInfo = new DirectoryInfo(dirPath);
            if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 || (dirInfo.Attributes & FileAttributes.System) != 0)
                return;

            if (IgnoredDirectoryNames.Contains(dirInfo.Name))
                return;

            foreach (var file in dirInfo.EnumerateFiles())
            {
                if ((file.Attributes & FileAttributes.Hidden) != 0 || (file.Attributes & FileAttributes.System) != 0)
                    continue;

                results.Add(new FileEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = file.Name,
                    Path = file.FullName,
                    Extension = file.Extension,
                    SizeBytes = file.Length,
                    IsDirectory = false,
                    ModifiedTime = new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
                    IndexedAt = now
                });

                if (results.Count >= maxFiles)
                    return;
            }

            if (currentDepth < maxDepth)
            {
                foreach (var subDir in dirInfo.EnumerateDirectories())
                {
                    if ((subDir.Attributes & FileAttributes.Hidden) != 0 || (subDir.Attributes & FileAttributes.System) != 0)
                        continue;

                    if (IgnoredDirectoryNames.Contains(subDir.Name))
                        continue;

                    results.Add(new FileEntity
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = subDir.Name,
                        Path = subDir.FullName,
                        Extension = null,
                        SizeBytes = 0,
                        IsDirectory = true,
                        ModifiedTime = new DateTimeOffset(subDir.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
                        IndexedAt = now
                    });

                    if (results.Count >= maxFiles)
                        return;

                    ScanDirectoryRecursively(subDir.FullName, results, currentDepth + 1, maxDepth, maxFiles, now);
                    if (results.Count >= maxFiles)
                        return;
                }
            }
        }
        catch
        {
            // Ignore access denied / system protected paths
        }
    }

    public Task<IEnumerable<QueryResult>> QueryAsync(QueryContext context)
    {
        var rawQuery = context.SearchTerm?.Trim();
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return Task.FromResult<IEnumerable<QueryResult>>([]);
        }

        // 1. Check for Direct Path Navigation (e.g. C:\, D:\, /Users, .\, ~\)
        if (IsDirectPathQuery(rawQuery))
        {
            var directResults = HandleDirectPathQuery(rawQuery);
            if (directResults.Count > 0)
            {
                return Task.FromResult<IEnumerable<QueryResult>>(directResults);
            }
        }

        // 2. Parse Prefix Filters (folder:, dir:, f:, file:, pdf:, doc:, sheet:, slide:, img:, code:, zip:, ext:)
        bool? filterDirectoryOnly = null;
        HashSet<string>? filterExtensions = null;
        string query = rawQuery;

        if (rawQuery.StartsWith("folder:", StringComparison.OrdinalIgnoreCase) ||
            rawQuery.StartsWith("dir:", StringComparison.OrdinalIgnoreCase) ||
            rawQuery.StartsWith("f:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = true;
            query = rawQuery[(rawQuery.IndexOf(':') + 1)..].Trim();
        }
        else if (rawQuery.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = false;
            query = rawQuery["file:".Length..].Trim();
        }
        else if (rawQuery.StartsWith("pdf:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = false;
            filterExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf" };
            query = rawQuery["pdf:".Length..].Trim();
        }
        else if (rawQuery.StartsWith("doc:", StringComparison.OrdinalIgnoreCase) ||
                 rawQuery.StartsWith("docs:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = false;
            filterExtensions = new(StringComparer.OrdinalIgnoreCase) { ".docx", ".doc", ".pdf", ".txt", ".md", ".rtf", ".odt" };
            query = rawQuery[(rawQuery.IndexOf(':') + 1)..].Trim();
        }
        else if (rawQuery.StartsWith("sheet:", StringComparison.OrdinalIgnoreCase) ||
                 rawQuery.StartsWith("excel:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = false;
            filterExtensions = new(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xls", ".csv", ".tsv" };
            query = rawQuery[(rawQuery.IndexOf(':') + 1)..].Trim();
        }
        else if (rawQuery.StartsWith("slide:", StringComparison.OrdinalIgnoreCase) ||
                 rawQuery.StartsWith("ppt:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = false;
            filterExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pptx", ".ppt" };
            query = rawQuery[(rawQuery.IndexOf(':') + 1)..].Trim();
        }
        else if (rawQuery.StartsWith("img:", StringComparison.OrdinalIgnoreCase) ||
                 rawQuery.StartsWith("photo:", StringComparison.OrdinalIgnoreCase) ||
                 rawQuery.StartsWith("pic:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = false;
            filterExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg", ".bmp", ".ico" };
            query = rawQuery[(rawQuery.IndexOf(':') + 1)..].Trim();
        }
        else if (rawQuery.StartsWith("code:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = false;
            filterExtensions = new(StringComparer.OrdinalIgnoreCase) { ".cs", ".py", ".js", ".ts", ".html", ".css", ".json", ".xml", ".sql", ".cpp", ".c", ".h" };
            query = rawQuery["code:".Length..].Trim();
        }
        else if (rawQuery.StartsWith("zip:", StringComparison.OrdinalIgnoreCase) ||
                 rawQuery.StartsWith("archive:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = false;
            filterExtensions = new(StringComparer.OrdinalIgnoreCase) { ".zip", ".rar", ".7z", ".tar", ".gz" };
            query = rawQuery[(rawQuery.IndexOf(':') + 1)..].Trim();
        }
        else if (rawQuery.StartsWith("ext:", StringComparison.OrdinalIgnoreCase))
        {
            filterDirectoryOnly = false;
            var extPart = rawQuery["ext:".Length..].Trim();
            var spaceIdx = extPart.IndexOf(' ');
            if (spaceIdx > 0)
            {
                var targetExt = extPart[..spaceIdx].TrimStart('.');
                filterExtensions = new(StringComparer.OrdinalIgnoreCase) { "." + targetExt };
                query = extPart[spaceIdx..].Trim();
            }
            else
            {
                var targetExt = extPart.TrimStart('.');
                filterExtensions = new(StringComparer.OrdinalIgnoreCase) { "." + targetExt };
                query = string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            // If user just typed prefix (e.g. "dir:" or "pdf:"), list all recent matching items
            List<FileEntity> initialSnapshot;
            lock (_lock)
            {
                initialSnapshot = [.. _cachedFiles];
            }

            var prefixInitial = initialSnapshot
                .Where(f => (filterDirectoryOnly == null || f.IsDirectory == filterDirectoryOnly.Value) &&
                            (filterExtensions == null || filterExtensions.Contains(f.Extension ?? string.Empty)))
                .Take(25)
                .Select(file => CreateFileQueryResult(
                    id: file.Id,
                    name: file.Name,
                    path: file.Path,
                    extension: file.Extension,
                    subText: file.IsDirectory ? $"{file.Path} • Folder" : $"{file.Path} • {FormatFileSize(file.SizeBytes)}",
                    score: 50.0,
                    isDir: file.IsDirectory,
                    isSemanticMatch: false
                ));

            return Task.FromResult<IEnumerable<QueryResult>>(prefixInitial);
        }

        // 3. Local Semantic Analysis (Zero-NPU Natural Language File Matcher)
        var semanticIntent = SemanticMatcher.AnalyzeQuery(query);

        // 4. Fuzzy & Semantic Search in cached files and folders
        List<FileEntity> snapshot;
        lock (_lock)
        {
            snapshot = [.. _cachedFiles];
        }

        var results = new List<QueryResult>();

        foreach (var file in snapshot)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            if (filterDirectoryOnly.HasValue && file.IsDirectory != filterDirectoryOnly.Value)
                continue;

            if (filterExtensions != null && !filterExtensions.Contains(file.Extension ?? string.Empty))
                continue;

            var fuzzyScore = FuzzyMatcher.Score(file.Name, query);
            var semanticScore = SemanticMatcher.EvaluateFileSemanticScore(file.Name, file.Extension, file.Path, semanticIntent);
            var combinedScore = fuzzyScore + semanticScore;

            if (combinedScore >= 35.0 || semanticScore >= 35.0)
            {
                var isDir = file.IsDirectory;
                var isSemantic = semanticScore >= 25.0;
                var semanticBadge = isSemantic ? " [🎯 AI Semantic]" : "";
                var subText = isDir ? $"{file.Path} • Folder{semanticBadge}" : $"{file.Path} • {FormatFileSize(file.SizeBytes)}{semanticBadge}";

                var result = CreateFileQueryResult(
                    id: file.Id,
                    name: file.Name,
                    path: file.Path,
                    extension: file.Extension,
                    subText: subText,
                    score: isDir ? combinedScore + 2.0 : combinedScore,
                    isDir: isDir,
                    isSemanticMatch: isSemantic
                );

                results.Add(result);
            }
        }

        return Task.FromResult<IEnumerable<QueryResult>>(results);
    }

    private static bool IsDirectPathQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;

        if (query.Length >= 2 && char.IsLetter(query[0]) && query[1] == ':')
            return true;

        if (query.StartsWith(@"\\") || query.StartsWith("/") || query.StartsWith(@"\"))
            return true;

        if (query.StartsWith("~") || query.StartsWith(@".\") || query.StartsWith(@"..\"))
            return true;

        return false;
    }

    private List<QueryResult> HandleDirectPathQuery(string inputPath)
    {
        var results = new List<QueryResult>();
        try
        {
            string resolvedPath = inputPath;
            if (resolvedPath.StartsWith("~"))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                resolvedPath = Path.Combine(userProfile, resolvedPath.TrimStart('~', '/', '\\'));
            }

            // Check if exact directory exists
            if (Directory.Exists(resolvedPath))
            {
                var dirInfo = new DirectoryInfo(resolvedPath);
                foreach (var dir in dirInfo.EnumerateDirectories())
                {
                    if ((dir.Attributes & FileAttributes.Hidden) != 0) continue;
                    results.Add(CreateFileQueryResult(
                        id: dir.FullName,
                        name: dir.Name,
                        path: dir.FullName,
                        extension: null,
                        subText: $"{dir.FullName} • Folder",
                        score: 250.0,
                        isDir: true,
                        isSemanticMatch: false
                    ));
                    if (results.Count >= 40) break;
                }

                foreach (var file in dirInfo.EnumerateFiles())
                {
                    if ((file.Attributes & FileAttributes.Hidden) != 0) continue;
                    results.Add(CreateFileQueryResult(
                        id: file.FullName,
                        name: file.Name,
                        path: file.FullName,
                        extension: file.Extension,
                        subText: $"{file.FullName} • {FormatFileSize(file.Length)}",
                        score: 240.0,
                        isDir: false,
                        isSemanticMatch: false
                    ));
                    if (results.Count >= 80) break;
                }
            }
            else
            {
                // Parent directory search with prefix
                var parent = Path.GetDirectoryName(resolvedPath);
                var pattern = Path.GetFileName(resolvedPath);

                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    var parentInfo = new DirectoryInfo(parent);
                    foreach (var dir in parentInfo.EnumerateDirectories())
                    {
                        if ((dir.Attributes & FileAttributes.Hidden) != 0) continue;
                        if (string.IsNullOrEmpty(pattern) || dir.Name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(CreateFileQueryResult(
                                id: dir.FullName,
                                name: dir.Name,
                                path: dir.FullName,
                                extension: null,
                                subText: $"{dir.FullName} • Folder",
                                score: 230.0,
                                isDir: true,
                                isSemanticMatch: false
                            ));
                        }
                    }

                    foreach (var file in parentInfo.EnumerateFiles())
                    {
                        if ((file.Attributes & FileAttributes.Hidden) != 0) continue;
                        if (string.IsNullOrEmpty(pattern) || file.Name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(CreateFileQueryResult(
                                id: file.FullName,
                                name: file.Name,
                                path: file.FullName,
                                extension: file.Extension,
                                subText: $"{file.FullName} • {FormatFileSize(file.Length)}",
                                score: 220.0,
                                isDir: false,
                                isSemanticMatch: false
                            ));
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently ignore path parsing exceptions
        }

        return results;
    }

    private QueryResult CreateFileQueryResult(string id, string name, string path, string? extension, string subText, double score, bool isDir, bool isSemanticMatch = false)
    {
        var glyph = GetGlyphForExtension(extension, isDir);
        var isExecutable = !isDir && !string.IsNullOrEmpty(extension) && ExecutableExtensions.Contains(extension);

        var actions = new List<ResultAction>
        {
            ActionRunner.CreateProcessAction(path, null, runAsAdmin: false, customName: isDir ? "Open Folder" : "Open")
        };

        if (isExecutable)
        {
            actions.Add(ActionRunner.CreateProcessAction(path, null, runAsAdmin: true));
        }
        else if (isDir)
        {
            // Open terminal as admin in directory
            actions.Add(new ResultAction
            {
                Name = "Open Terminal as Admin here",
                Description = "Launch PowerShell as administrator in this directory",
                IconGlyph = "🛡️",
                ShortcutHint = "Ctrl+Shift+Enter",
                SyncAction = () =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoExit -Command \"Set-Location -LiteralPath '{path}'\"",
                        Verb = "runas",
                        UseShellExecute = true
                    });
                }
            });

            // Open normal terminal in directory
            actions.Add(new ResultAction
            {
                Name = "Open in Terminal",
                Description = "Launch PowerShell in this directory",
                IconGlyph = "💻",
                ShortcutHint = "Alt+Enter",
                SyncAction = () =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoExit -Command \"Set-Location -LiteralPath '{path}'\"",
                        UseShellExecute = true
                    });
                }
            });

            // Open in VS Code
            actions.Add(new ResultAction
            {
                Name = "Open in VS Code",
                Description = "Open directory in Visual Studio Code",
                IconGlyph = "📝",
                ShortcutHint = "Ctrl+Shift+V",
                SyncAction = () =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c code \"{path}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            });
        }
        else
        {
            // AI Copilot Actions for Files (RAG & Local Summarization)
            if (IsTextOrDocumentExtension(extension))
            {
                actions.Add(new ResultAction
                {
                    Name = "✨ Tóm tắt tài liệu với AI",
                    Description = "Trích xuất ý chính của tệp với mô hình Ollama cục bộ",
                    IconGlyph = "🤖",
                    ShortcutHint = "Ctrl+Alt+S",
                    SyncAction = () => ExecuteAiSummarizeFile(path, name)
                });

                actions.Add(new ResultAction
                {
                    Name = "💬 Chat với tài liệu này (Local RAG)",
                    Description = "Đặt câu hỏi trực tiếp dựa trên nội dung tệp",
                    IconGlyph = "💡",
                    ShortcutHint = "Ctrl+Alt+C",
                    SyncAction = () => ExecuteAiChatFile(path, name)
                });
            }
        }

        actions.Add(ActionRunner.CreateOpenFolderAction(path));
        actions.Add(ActionRunner.CreateCopyPathAction(path));

        return new QueryResult
        {
            Id = id,
            Title = name,
            Subtitle = subText,
            Score = score,
            Type = isDir ? ResultType.Folder : ResultType.File,
            IconPath = path,
            IconGlyph = glyph,
            Metadata = new Dictionary<string, string>
            {
                ["Path"] = path,
                ["Extension"] = extension ?? string.Empty,
                ["IsDirectory"] = isDir.ToString(),
                ["IsSemanticMatch"] = isSemanticMatch.ToString()
            },
            Actions = actions
        };
    }

    private static bool IsTextOrDocumentExtension(string? ext)
    {
        if (string.IsNullOrEmpty(ext)) return false;
        var e = ext.ToLowerInvariant();
        return e is ".txt" or ".md" or ".json" or ".csv" or ".xml" or ".log" or ".sql"
            or ".cs" or ".py" or ".js" or ".ts" or ".html" or ".css" or ".cpp" or ".c" or ".h"
            or ".docx" or ".pdf" or ".rtf";
    }

    private void ExecuteAiSummarizeFile(string filePath, string fileName)
    {
        try
        {
            var content = ReadFileContentSnippet(filePath, maxChars: 6000);
            var prompt = $"Hãy đọc và tóm tắt ngắn gọn, súc tích các ý chính của tài liệu '{fileName}':\n\n```\n{content}\n```";
            OnRequestOpenAiWithFile?.Invoke(prompt, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileIndexer] AI Summarize error: {ex.Message}");
        }
    }

    private void ExecuteAiChatFile(string filePath, string fileName)
    {
        try
        {
            var content = ReadFileContentSnippet(filePath, maxChars: 6000);
            var prompt = $"Tôi muốn hỏi về tài liệu '{fileName}':\n\n```\n{content}\n```\n\nHãy giải thích ngắn gọn nội dung tổng quan của tài liệu này và những thông tin đáng chú ý nhất.";
            OnRequestOpenAiWithFile?.Invoke(prompt, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileIndexer] AI Chat error: {ex.Message}");
        }
    }

    private static string ReadFileContentSnippet(string filePath, int maxChars)
    {
        if (!File.Exists(filePath)) return string.Empty;
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
            return $"[Tài liệu: {Path.GetFileName(filePath)}]";
        }
    }

    private static string GetGlyphForExtension(string? ext, bool isDir)
    {
        if (isDir) return "📁";
        if (string.IsNullOrWhiteSpace(ext)) return "📄";

        ext = ext.ToLowerInvariant();
        return ext switch
        {
            ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" => "⚙️",
            ".txt" or ".md" or ".doc" or ".docx" or ".pdf" or ".rtf" => "📄",
            ".xls" or ".xlsx" or ".csv" => "📊",
            ".ppt" or ".pptx" => "📽️",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".svg" or ".webp" or ".ico" => "🖼️",
            ".mp3" or ".wav" or ".flac" or ".m4a" or ".ogg" => "🎵",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" => "🎬",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
            ".cs" or ".cpp" or ".c" or ".h" or ".py" or ".js" or ".ts" or ".html" or ".css" or ".json" or ".xml" or ".sql" => "💻",
            _ => "📄"
        };
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double doubleBytes = bytes;

        while (doubleBytes >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            doubleBytes /= 1024;
            suffixIndex++;
        }

        return $"{doubleBytes:0.##} {suffixes[suffixIndex]}";
    }
}
