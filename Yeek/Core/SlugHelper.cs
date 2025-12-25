using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Yeek.Core;

public static partial class SlugHelper
{
    public static string GenerateSlug(string input, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Normalize and remove diacritics (é -> e, ü -> u)
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = char.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var cleaned = sb
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        // Replace anything that's not a letter or digit with a dash
        cleaned = CleanRegex().Replace(cleaned, "-");

        // Trim extra dashes
        cleaned = cleaned.Trim('-');

        // cut off if too long
        if (cleaned.Length > maxLength)
            cleaned = cleaned[..maxLength].Trim('-');

        return cleaned;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex CleanRegex();
}