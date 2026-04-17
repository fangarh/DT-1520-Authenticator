namespace OtpAuth.Application.Devices;

public static class DeviceRefreshTokenFormat
{
    private const string Prefix = "drt_";

    public static string Create(Guid tokenId, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return $"{Prefix}{tokenId:N}.{secret.Trim()}";
    }

    public static bool TryParse(string? rawValue, out Guid tokenId, out string? secret)
    {
        tokenId = Guid.Empty;
        secret = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var trimmed = rawValue.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = trimmed.IndexOf('.', Prefix.Length);
        if (separatorIndex <= Prefix.Length)
        {
            return false;
        }

        var tokenIdPart = trimmed.Substring(Prefix.Length, separatorIndex - Prefix.Length);
        if (!Guid.TryParseExact(tokenIdPart, "N", out tokenId))
        {
            return false;
        }

        var parsedSecret = trimmed[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(parsedSecret))
        {
            tokenId = Guid.Empty;
            return false;
        }

        secret = parsedSecret;
        return true;
    }
}
