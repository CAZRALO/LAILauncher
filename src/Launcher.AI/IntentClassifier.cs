using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Launcher.AI;

public class IntentClassifier : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly Dictionary<string, int> _vocab = new(StringComparer.OrdinalIgnoreCase);
    private readonly float[]? _idf;
    private readonly List<string> _labels = [];
    private readonly int _ngramMax = 2;
    private readonly bool _isModelReady;

    public bool IsModelLoaded => _isModelReady;

    public IntentClassifier(string? modelPath = null, string? vocabPath = null)
    {
        try
        {
            var resolvedModelPath = ResolveFilePath(modelPath, "intent_classifier.onnx");
            var resolvedVocabPath = ResolveFilePath(vocabPath, "vocab.json");

            if (!string.IsNullOrEmpty(resolvedModelPath) && File.Exists(resolvedModelPath) &&
                !string.IsNullOrEmpty(resolvedVocabPath) && File.Exists(resolvedVocabPath))
            {
                // 1. Load Vocab & Config
                var vocabJson = File.ReadAllText(resolvedVocabPath);
                using var doc = JsonDocument.Parse(vocabJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("vocab", out var vocabProp))
                {
                    foreach (var prop in vocabProp.EnumerateObject())
                    {
                        _vocab[prop.Name] = prop.Value.GetInt32();
                    }
                }

                if (root.TryGetProperty("idf", out var idfProp))
                {
                    _idf = new float[idfProp.GetArrayLength()];
                    int idx = 0;
                    foreach (var el in idfProp.EnumerateArray())
                    {
                        _idf[idx++] = (float)el.GetDouble();
                    }
                }

                if (root.TryGetProperty("labels", out var labelsProp))
                {
                    foreach (var el in labelsProp.EnumerateArray())
                    {
                        _labels.Add(el.GetString() ?? string.Empty);
                    }
                }

                if (root.TryGetProperty("ngram_max", out var ngramProp))
                {
                    _ngramMax = ngramProp.GetInt32();
                }

                // 2. Initialize InferenceSession
                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
                };
                _session = new InferenceSession(resolvedModelPath, sessionOptions);
                _isModelReady = _vocab.Count > 0 && _idf != null && _labels.Count > 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IntentClassifier] ONNX load failed: {ex.Message}");
            _isModelReady = false;
        }
    }

    private static string? ResolveFilePath(string? explicitPath, string defaultFileName)
    {
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
        {
            return explicitPath;
        }

        // Check in AppDomain base directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var cand1 = Path.Combine(baseDir, "Models", defaultFileName);
        if (File.Exists(cand1)) return cand1;

        var cand2 = Path.Combine(baseDir, defaultFileName);
        if (File.Exists(cand2)) return cand2;

        // Traverse upwards for development/solution directories
        var dir = new DirectoryInfo(baseDir);
        for (int i = 0; i < 5 && dir != null; i++)
        {
            var p1 = Path.Combine(dir.FullName, "src", "Launcher.AI", "Models", defaultFileName);
            if (File.Exists(p1)) return p1;

            var p2 = Path.Combine(dir.FullName, "ml", "intent_classifier", defaultFileName);
            if (File.Exists(p2)) return p2;

            dir = dir.Parent;
        }

        return null;
    }

    public IntentResult Predict(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new IntentResult { Intent = IntentType.App, Confidence = 0f };
        }

        var q = query.Trim();

        // 1. High-confidence explicit prefix detection (Fast-path < 0.1ms)
        if (q.StartsWith(">") || q.StartsWith("cmd ", StringComparison.OrdinalIgnoreCase) || q.StartsWith("ps ", StringComparison.OrdinalIgnoreCase))
        {
            return new IntentResult { Intent = IntentType.Command, Confidence = 0.99f, ExtractedEntity = q.TrimStart('>', ' ') };
        }

        if (q.StartsWith("calc ", StringComparison.OrdinalIgnoreCase) || 
            q.StartsWith("calc:", StringComparison.OrdinalIgnoreCase) || 
            q.Equals("calc", StringComparison.OrdinalIgnoreCase) ||
            q.StartsWith("=") ||
            (Regex.IsMatch(q, @"^[\d\s\+\-\*\/\^\%\(\)\.\,]+$") && q.Any(c => c is '+' or '-' or '*' or '/' or '^' or '%')))
        {
            return new IntentResult { Intent = IntentType.Calculator, Confidence = 0.99f, ExtractedEntity = q };
        }

        if (q.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            q.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(q, @"^[a-zA-Z0-9\-]+\.(com|org|net|vn|io|dev|ai|app)($|\/)", RegexOptions.IgnoreCase))
        {
            return new IntentResult { Intent = IntentType.UrlWeb, Confidence = 0.99f, ExtractedEntity = q };
        }

        if (q.StartsWith("ai:", StringComparison.OrdinalIgnoreCase) || 
            q.StartsWith("?", StringComparison.OrdinalIgnoreCase) ||
            q.StartsWith("hỏi ai ", StringComparison.OrdinalIgnoreCase) ||
            q.StartsWith("hỏi ", StringComparison.OrdinalIgnoreCase) ||
            q.StartsWith("ask ai ", StringComparison.OrdinalIgnoreCase) ||
            q.StartsWith("ai ", StringComparison.OrdinalIgnoreCase))
        {
            var cleanEntity = Regex.Replace(q, @"^(ai:\s*|\?\s*|hỏi ai\s*|hỏi\s*|ask ai\s*|ai\s+)", "", RegexOptions.IgnoreCase).Trim();
            return new IntentResult { Intent = IntentType.AI, Confidence = 0.99f, ExtractedEntity = cleanEntity };
        }

        if (q.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || q.StartsWith("f:", StringComparison.OrdinalIgnoreCase))
        {
            return new IntentResult { Intent = IntentType.File, Confidence = 0.99f, ExtractedEntity = q.Substring(q.IndexOf(':') + 1).Trim() };
        }

        // 2. ML ONNX Inference Engine
        if (_isModelReady && _session != null && _idf != null)
        {
            try
            {
                var features = ExtractTfIdfFeatures(q);
                var tensor = new DenseTensor<float>(features, new[] { 1, features.Length });
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("features", tensor)
                };

                using var results = _session.Run(inputs);
                var outputLabelTensor = results.FirstOrDefault(r => r.Name == "output_label");

                if (outputLabelTensor != null)
                {
                    var labelIdx = (int)outputLabelTensor.AsTensor<long>().First();
                    if (labelIdx >= 0 && labelIdx < _labels.Count)
                    {
                        var labelName = _labels[labelIdx];
                        var intent = MapLabelToIntent(labelName);
                        return new IntentResult
                        {
                            Intent = intent,
                            Confidence = 0.92f,
                            ExtractedEntity = q
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IntentClassifier] Inference error: {ex.Message}");
            }
        }

        // 3. Fallback to App intent
        return new IntentResult { Intent = IntentType.App, Confidence = 0.6f, ExtractedEntity = q };
    }

    private float[] ExtractTfIdfFeatures(string text)
    {
        var numFeatures = _idf!.Length;
        var features = new float[numFeatures];

        // Tokenization matching python pattern: r"(?u)\b\w+\b|[><=?:/\-]+"
        var matches = Regex.Matches(text.ToLowerInvariant(), @"\b\w+\b|[><=?:/\-]+");
        var tokens = new List<string>();
        foreach (Match m in matches)
        {
            if (m.Success && !string.IsNullOrWhiteSpace(m.Value))
            {
                tokens.Add(m.Value);
            }
        }

        if (tokens.Count == 0)
        {
            return features;
        }

        // Count term frequencies for unigrams and bigrams
        var termCounts = new Dictionary<int, int>();

        // 1-grams
        for (int i = 0; i < tokens.Count; i++)
        {
            if (_vocab.TryGetValue(tokens[i], out var featIdx))
            {
                termCounts[featIdx] = termCounts.GetValueOrDefault(featIdx, 0) + 1;
            }

            // 2-grams
            if (_ngramMax >= 2 && i + 1 < tokens.Count)
            {
                var bigram = tokens[i] + " " + tokens[i + 1];
                if (_vocab.TryGetValue(bigram, out var bigramIdx))
                {
                    termCounts[bigramIdx] = termCounts.GetValueOrDefault(bigramIdx, 0) + 1;
                }
            }
        }

        // Calculate TF * IDF
        float sumSq = 0f;
        foreach (var (featIdx, tf) in termCounts)
        {
            if (featIdx >= 0 && featIdx < numFeatures)
            {
                var val = tf * _idf[featIdx];
                features[featIdx] = val;
                sumSq += val * val;
            }
        }

        // L2 normalization (standard in scikit-learn TfidfVectorizer)
        if (sumSq > 0f)
        {
            var norm = MathF.Sqrt(sumSq);
            for (int i = 0; i < numFeatures; i++)
            {
                if (features[i] != 0f)
                {
                    features[i] /= norm;
                }
            }
        }

        return features;
    }

    private static IntentType MapLabelToIntent(string label)
    {
        return label.ToUpperInvariant() switch
        {
            "APP" => IntentType.App,
            "FILE" => IntentType.File,
            "CALCULATOR" => IntentType.Calculator,
            "URL_WEB" => IntentType.UrlWeb,
            "COMMAND" => IntentType.Command,
            "AI" => IntentType.AI,
            _ => IntentType.App
        };
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
