
using System.Text;
using System.Text.RegularExpressions;

namespace Ecommerce.Application_BAL_.Helpers;

public static class SlugGenerator
{
    public static string Generate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty.", nameof(value));

        string slug = value.Trim().ToLowerInvariant();

        // Normalize Unicode characters.
        string normalized = slug.Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (char character in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo
                .GetUnicodeCategory(character);

            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        slug = builder.ToString().Normalize(NormalizationForm.FormC);

        // Replace spaces and underscores with hyphens.
        slug = Regex.Replace(slug, @"[\s_]+", "-");

        // Keep only letters, numbers, and hyphens.
        slug = Regex.Replace(slug, @"[^a-z0-9-]", "");

        // Remove consecutive hyphens.
        slug = Regex.Replace(slug, @"-+", "-");

        // Remove leading and trailing hyphens.
        slug = slug.Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Could not generate a valid slug.", nameof(value));

        return slug;
    }
}
