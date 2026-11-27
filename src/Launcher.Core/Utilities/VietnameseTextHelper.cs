using System.Globalization;
using System.Text;

namespace Launcher.Core.Utilities;

public static class VietnameseTextHelper
{
    /// <summary>
    /// Removes Vietnamese diacritics and accent marks.
    /// Fast-paths pure ASCII text with zero allocations.
    /// </summary>
    public static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Fast-path: If all characters are ASCII and neither 'đ' nor 'Đ', return original string (0 allocation)
        bool hasNonAscii = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= 128)
            {
                hasNonAscii = true;
                break;
            }
        }

        if (!hasNonAscii)
            return text;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        Span<char> buffer = normalizedString.Length <= 256 ? stackalloc char[normalizedString.Length] : new char[normalizedString.Length];
        int len = 0;

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                if (c is 'đ')
                    buffer[len++] = 'd';
                else if (c is 'Đ')
                    buffer[len++] = 'D';
                else
                    buffer[len++] = c;
            }
        }

        return new string(buffer[..len]).Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Fast check if text has any non-ASCII character.
    /// </summary>
    public static bool HasNonAscii(ReadOnlySpan<char> text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] >= 128) return true;
        }
        return false;
    }
}
