using System.Security.Cryptography;

namespace OtpAuth.Infrastructure.Factors;

public static class BootstrapTotpSecretProvider
{
    public static byte[] LoadFromEnvironmentOrRandom(string? base64Secret)
    {
        if (string.IsNullOrWhiteSpace(base64Secret))
        {
            return RandomNumberGenerator.GetBytes(32);
        }

        try
        {
            var secret = Convert.FromBase64String(base64Secret);
            if (secret.Length < 16)
            {
                throw new InvalidOperationException(
                    "Bootstrap TOTP master secret must decode to at least 16 bytes.");
            }

            return secret;
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Bootstrap TOTP master secret must be a valid base64 string.",
                exception);
        }
    }
}
