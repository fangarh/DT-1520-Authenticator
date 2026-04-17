using System.Security.Cryptography;
using System.Text;

namespace OtpAuth.Application.Factors;

public static class TotpCodeCalculator
{
    public static bool IsCodeValid(
        byte[] secret,
        int digits,
        int periodSeconds,
        string algorithm,
        string code,
        DateTimeOffset timestamp,
        int allowedTimeStepSkew = 1)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var normalizedCode = code.Trim();
        var currentStep = GetTimeStep(timestamp, periodSeconds);

        for (var offset = -allowedTimeStepSkew; offset <= allowedTimeStepSkew; offset++)
        {
            var expectedCode = GenerateCode(secret, digits, algorithm, currentStep + offset);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expectedCode),
                    Encoding.ASCII.GetBytes(normalizedCode)))
            {
                return true;
            }
        }

        return false;
    }

    public static string GenerateCode(
        byte[] secret,
        int digits,
        string algorithm,
        long timeStep)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);

        if (digits != 6)
        {
            throw new InvalidOperationException("Only 6-digit TOTP enrollments are currently supported.");
        }

        if (!string.Equals(algorithm, "SHA1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only SHA1 TOTP enrollments are currently supported.");
        }

        Span<byte> counterBytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(counterBytes, timeStep);

        if (BitConverter.IsLittleEndian)
        {
            counterBytes.Reverse();
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    public static long GetTimeStep(DateTimeOffset timestamp, int periodSeconds)
    {
        if (periodSeconds <= 0)
        {
            throw new InvalidOperationException("TOTP period must be greater than zero.");
        }

        return timestamp.ToUnixTimeSeconds() / periodSeconds;
    }
}
