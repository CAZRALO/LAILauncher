using System.Globalization;
using System.Text;

namespace Launcher.Core.Utilities;

public static class VietnameseTextHelper
{
    public static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                if (c is 'đ')
                    stringBuilder.Append('d');
                else if (c is 'Đ')
                    stringBuilder.Append('D');
                else
                    stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
