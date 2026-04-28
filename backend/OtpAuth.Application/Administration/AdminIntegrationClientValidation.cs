using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Administration;

internal static class AdminIntegrationClientValidation
{
    private const int MaxClientIdLength = 200;

    private static readonly string[] SupportedScopes =
    [
        IntegrationClientScopes.ChallengesRead,
        IntegrationClientScopes.ChallengesWrite,
        IntegrationClientScopes.EnrollmentsWrite,
        IntegrationClientScopes.DevicesWrite,
    ];

    public static string? NormalizeClientId(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        var normalized = clientId.Trim();
        if (normalized.Length > MaxClientIdLength)
        {
            return null;
        }

        return normalized.All(IsRouteSafeClientIdCharacter)
            ? normalized
            : null;
    }

    public static IReadOnlyCollection<string> NormalizeScopes(
        IReadOnlyCollection<string> scopes,
        out string? error)
    {
        if (scopes.Count == 0)
        {
            error = "At least one allowed scope is required.";
            return Array.Empty<string>();
        }

        var normalized = new List<string>();
        foreach (var scope in scopes)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                error = "Allowed scopes cannot contain empty values.";
                return Array.Empty<string>();
            }

            var trimmed = scope.Trim();
            if (!SupportedScopes.Contains(trimmed, StringComparer.Ordinal))
            {
                error = $"Unsupported integration scope '{trimmed}'.";
                return Array.Empty<string>();
            }

            if (!normalized.Contains(trimmed, StringComparer.Ordinal))
            {
                normalized.Add(trimmed);
            }
        }

        error = null;
        return normalized
            .OrderBy(static scope => scope, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsRouteSafeClientIdCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) ||
               character is '.' or '_' or '-';
    }
}
