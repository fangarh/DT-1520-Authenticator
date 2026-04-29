namespace OtpAuth.Application.Administration;

internal static class AdminTenantDirectoryValidation
{
    private const int MaxDisplayNameLength = 200;
    private const int MaxSlugLength = 120;

    public static string? NormalizeDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var normalized = displayName.Trim();
        return normalized.Length <= MaxDisplayNameLength
            ? normalized
            : null;
    }

    public static string? NormalizeSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalized = slug.Trim().ToLowerInvariant();
        if (normalized.Length > MaxSlugLength)
        {
            return null;
        }

        return normalized.All(IsSlugCharacter)
            ? normalized
            : null;
    }

    public static string CreateSlugCandidate(string displayName, string fallback)
    {
        var normalized = new List<char>(displayName.Length);
        var previousWasSeparator = false;
        foreach (var character in displayName.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                normalized.Add(character);
                previousWasSeparator = false;
                continue;
            }

            if (character is '-' or '_' or '.' || char.IsWhiteSpace(character))
            {
                if (!previousWasSeparator && normalized.Count > 0)
                {
                    normalized.Add('-');
                    previousWasSeparator = true;
                }
            }
        }

        var slug = new string(normalized.ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = fallback;
        }

        return slug.Length <= MaxSlugLength
            ? slug
            : slug[..MaxSlugLength].Trim('-');
    }

    private static bool IsSlugCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) ||
               character is '-' or '_';
    }
}
