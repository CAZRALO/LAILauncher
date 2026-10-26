using Launcher.Core.Utilities;

namespace Launcher.Core.Search;

public static class FuzzyMatcher
{
    private static readonly char[] WordSeparators = [' ', '-', '_', '.', '/', '\\'];

    /// <summary>
    /// Computes a fuzzy matching score between 0.0 and 100.0.
    /// Higher is better match. Returns 0 if no match found.
    /// </summary>
    public static double Score(string target, string query)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(query))
            return 0.0;

        var rawScore = ComputeScore(target, query);
        if (rawScore >= 99.0)
            return rawScore;

        // Vietnamese / Accent-insensitive evaluation
        var unaccentTarget = VietnameseTextHelper.RemoveDiacritics(target);
        var unaccentQuery = VietnameseTextHelper.RemoveDiacritics(query);

        if (!string.Equals(unaccentTarget, target, StringComparison.Ordinal) ||
            !string.Equals(unaccentQuery, query, StringComparison.Ordinal))
        {
            var unaccentScore = ComputeScore(unaccentTarget, unaccentQuery);
            return Math.Max(rawScore, unaccentScore * 0.99);
        }

        return rawScore;
    }

    private static double ComputeScore(string target, string query)
    {
        var t = target.Trim();
        var q = query.Trim();

        // 1. Exact match
        if (string.Equals(t, q, StringComparison.OrdinalIgnoreCase))
            return 100.0;

        // 2. Normalized match (punctuation and whitespace agnostic, e.g. "cpuinfo" matches "CPU-Info", "scrbleink" matches "Scrble Ink")
        var tNorm = NormalizeString(t);
        var qNorm = NormalizeString(q);

        if (!string.IsNullOrEmpty(tNorm) && !string.IsNullOrEmpty(qNorm))
        {
            if (string.Equals(tNorm, qNorm, StringComparison.OrdinalIgnoreCase))
                return 99.0;

            if (tNorm.StartsWith(qNorm, StringComparison.OrdinalIgnoreCase))
                return 92.0 + Math.Max(0, 7.0 - (tNorm.Length - qNorm.Length) * 0.35);
        }

        // 3. Prefix match
        if (t.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return 90.0 + Math.Max(0, 10.0 - (t.Length - q.Length) * 0.5);

        var words = t.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

        // 4. Exact word match (e.g. "Gemini" in "Google Gemini" matching "gemini")
        foreach (var word in words)
        {
            if (string.Equals(word, q, StringComparison.OrdinalIgnoreCase))
            {
                return 95.0 + Math.Max(0, 4.0 - (words.Length - 1) * 0.5);
            }
        }

        // 5. Word prefix match (e.g. "gem" matching "Google Gemini", "vs" matching "Visual Studio")
        foreach (var word in words)
        {
            if (word.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            {
                return 85.0 + Math.Max(0, 5.0 - (word.Length - q.Length) * 0.5);
            }
        }

        // 6. Typo Tolerance via Levenshtein Distance (e.g. "notepd" -> "Notepad", "scrible" -> "Scrble")
        if (q.Length >= 4)
        {
            foreach (var word in words)
            {
                var dist = ComputeLevenshtein(word, q);
                if (dist == 1 && Math.Abs(word.Length - q.Length) <= 1)
                {
                    return 82.0 + Math.Max(0, 4.0 - (words.Length - 1) * 0.5);
                }
                if (dist == 2 && word.Length >= 6 && q.Length >= 6)
                {
                    return 70.0;
                }
            }
        }

        // 7. Multi-token query match (e.g. "gg gemini", "vs code", "arc browser", "scrble ink")
        var qTokens = q.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (qTokens.Length > 1)
        {
            var tokenScoreSum = 0.0;
            var matchedTokens = 0;

            foreach (var token in qTokens)
            {
                var bestTokenScore = 0.0;
                foreach (var word in words)
                {
                    if (string.Equals(word, token, StringComparison.OrdinalIgnoreCase))
                    {
                        bestTokenScore = Math.Max(bestTokenScore, 95.0);
                    }
                    else if (word.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                    {
                        bestTokenScore = Math.Max(bestTokenScore, 85.0);
                    }
                    else if (word.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        bestTokenScore = Math.Max(bestTokenScore, 70.0);
                    }
                    else if (token.Length >= 4 && ComputeLevenshtein(word, token) <= 1)
                    {
                        bestTokenScore = Math.Max(bestTokenScore, 80.0);
                    }
                }

                if (bestTokenScore > 0.0)
                {
                    matchedTokens++;
                    tokenScoreSum += bestTokenScore;
                }
            }

            if (matchedTokens == qTokens.Length)
            {
                var avg = tokenScoreSum / qTokens.Length;
                return Math.Min(98.0, avg);
            }
            else if (matchedTokens > 0)
            {
                // Partial multi-token match: at least one token matched well
                var ratio = (double)matchedTokens / qTokens.Length;
                var avg = (tokenScoreSum / qTokens.Length) * (0.6 + 0.4 * ratio);
                if (avg >= 35.0)
                {
                    return avg;
                }
            }
        }

        // 8. Acronym match (e.g. "vs" for "Visual Studio", "vsc" for "Visual Studio Code")
        if (words.Length > 1 && q.Length <= words.Length)
        {
            var acronym = new string(words.Where(w => w.Length > 0).Select(w => w[0]).ToArray());
            if (acronym.StartsWith(q, StringComparison.OrdinalIgnoreCase))
                return 75.0 + Math.Max(0, 5.0 - (words.Length - q.Length) * 0.5);
        }

        // 9. Substring match
        var substringIndex = t.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        if (substringIndex >= 0)
        {
            return 65.0 + Math.Max(0, 10.0 - substringIndex * 0.5);
        }

        // 9b. Normalized substring match (ignoring separators)
        if (!string.IsNullOrEmpty(tNorm) && !string.IsNullOrEmpty(qNorm))
        {
            var normIdx = tNorm.IndexOf(qNorm, StringComparison.OrdinalIgnoreCase);
            if (normIdx >= 0)
            {
                return 68.0 + Math.Max(0, 10.0 - normIdx * 0.5);
            }
        }

        // 10. Subsequence match (characters appear in order)
        var qIdx = 0;
        var consecutiveBonus = 0;
        var totalScore = 0.0;

        for (var i = 0; i < t.Length && qIdx < q.Length; i++)
        {
            if (char.ToLowerInvariant(t[i]) == char.ToLowerInvariant(q[qIdx]))
            {
                qIdx++;
                consecutiveBonus++;
                totalScore += 5.0 + (consecutiveBonus * 2.0);
            }
            else
            {
                consecutiveBonus = 0;
            }
        }

        if (qIdx == q.Length)
        {
            var coverage = (double)q.Length / t.Length;
            return Math.Min(50.0, 20.0 + totalScore * coverage);
        }

        return 0.0;
    }

    private static string NormalizeString(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    private static int ComputeLevenshtein(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = char.ToLowerInvariant(s[i - 1]) == char.ToLowerInvariant(t[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}

