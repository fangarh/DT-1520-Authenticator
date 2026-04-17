using System.Security.Cryptography;
using OtpAuth.Application.Devices;

namespace OtpAuth.Infrastructure.Devices;

public sealed class Pbkdf2DeviceRefreshTokenHasher : IDeviceRefreshTokenHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Hash(string tokenSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenSecret);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(tokenSecret, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string tokenSecret, string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenSecret) || string.IsNullOrWhiteSpace(tokenHash))
        {
            return false;
        }

        var parts = tokenHash.Split('$', StringSplitOptions.None);
        if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(tokenSecret, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
