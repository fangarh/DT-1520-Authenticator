namespace OtpAuth.Application.Factors;

public static class BackupCodeFormat
{
    private const int MinLength = 8;
    private const int MaxLength = 32;

    public static bool TryNormalize(
        string? rawCode,
        out string normalizedCode,
        out string? validationError)
    {
        normalizedCode = string.Empty;
        validationError = null;

        if (string.IsNullOrWhiteSpace(rawCode))
        {
            validationError = "Backup code is required.";
            return false;
        }

        var filteredCharacters = rawCode
            .Trim()
            .Where(character => !char.IsWhiteSpace(character) && character != '-')
            .ToArray();

        if (filteredCharacters.Length < MinLength || filteredCharacters.Length > MaxLength)
        {
            validationError = $"Backup code must be {MinLength}-{MaxLength} alphanumeric characters.";
            return false;
        }

        if (filteredCharacters.Any(character => !char.IsLetterOrDigit(character)))
        {
            validationError = $"Backup code must be {MinLength}-{MaxLength} alphanumeric characters.";
            return false;
        }

        normalizedCode = new string(filteredCharacters).ToUpperInvariant();
        return true;
    }
}
