using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace team_hub_organization.Services.Organizations;

public static partial class SlugHelper
{
    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonSlugCharactersRegex();

    [GeneratedRegex(@"^-+|-+$", RegexOptions.Compiled)]
    private static partial Regex TrimHyphensRegex();

    public static string GenerateFromName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(character);
        }

        var slug = TrimHyphensRegex().Replace(NonSlugCharactersRegex().Replace(builder.ToString(), "-"), string.Empty);
        return string.IsNullOrWhiteSpace(slug) ? "organization" : slug;
    }

    public static async Task<string> EnsureUniqueSlugAsync(
        Func<string, Task<bool>> slugExistsAsync,
        string baseSlug,
        CancellationToken cancellationToken = default)
    {
        var candidate = baseSlug;
        var suffix = 2;

        while (await slugExistsAsync(candidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
