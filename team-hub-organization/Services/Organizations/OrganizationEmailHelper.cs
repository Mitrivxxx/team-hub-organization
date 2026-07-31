using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace team_hub_organization.Services.Organizations;

public static partial class OrganizationEmailHelper
{
    public const string Domain = "@teamhub.local";

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonLocalPartCharactersRegex();

    public static string LocalPartFromName(string name)
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

        var localPart = NonLocalPartCharactersRegex().Replace(builder.ToString(), string.Empty);
        return string.IsNullOrWhiteSpace(localPart) ? "organization" : localPart;
    }

    public static string BuildEmail(string localPart, int digits) =>
        $"{localPart}{digits:D4}{Domain}";

    public static async Task<string> GenerateUniqueEmailAsync(
        Func<string, Task<bool>> emailExistsAsync,
        string organizationName,
        CancellationToken cancellationToken = default)
    {
        var localPart = LocalPartFromName(organizationName);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var email = BuildEmail(localPart, Random.Shared.Next(0, 10000));
            if (!await emailExistsAsync(email))
                return email;
        }

        throw new InvalidOperationException("Could not generate a unique organization email.");
    }
}
