namespace OtpAuth.Application.Devices;

public static class DeviceActivationCodeFormat
{
    private const string Prefix = "dac_";

    public static string Create(Guid activationCodeId, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return $"{Prefix}{activationCodeId:N}.{secret.Trim()}";
    }

    public static bool TryParse(string? rawValue, out Guid activationCodeId, out string? secret)
    {
        activationCodeId = Guid.Empty;
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

        var activationCodeIdPart = trimmed.Substring(Prefix.Length, separatorIndex - Prefix.Length);
        if (!Guid.TryParseExact(activationCodeIdPart, "N", out activationCodeId))
        {
            return false;
        }

        var parsedSecret = trimmed[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(parsedSecret))
        {
            activationCodeId = Guid.Empty;
            return false;
        }

        secret = parsedSecret;
        return true;
    }
}
