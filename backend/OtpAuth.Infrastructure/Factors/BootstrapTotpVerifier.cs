using System.Security.Cryptography;
using System.Text;
using OtpAuth.Application.Factors;
using OtpAuth.Domain.Challenges;

namespace OtpAuth.Infrastructure.Factors;

public sealed class BootstrapTotpVerifier : ITotpVerifier
{
    private readonly byte[] _masterSecret;

    public BootstrapTotpVerifier(byte[] masterSecret)
    {
        if (masterSecret is null || masterSecret.Length < 16)
        {
            throw new ArgumentException("Master secret must be at least 16 bytes long.", nameof(masterSecret));
        }

        _masterSecret = masterSecret.ToArray();
    }

    public Task<TotpVerificationResult> VerifyAsync(
        Challenge challenge,
        string code,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedCode = code.Trim();
        var currentStep = GetTimeStep(timestamp);

        for (var offset = -1; offset <= 1; offset++)
        {
            var expectedCode = GenerateCode(challenge.ExternalUserId, currentStep + offset);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expectedCode),
                    Encoding.ASCII.GetBytes(normalizedCode)))
            {
                return Task.FromResult(TotpVerificationResult.Valid(Guid.Empty, currentStep + offset));
            }
        }

        return Task.FromResult(TotpVerificationResult.InvalidCode());
    }

    public string GenerateCodeForUser(string externalUserId, DateTimeOffset timestamp)
    {
        return GenerateCode(externalUserId, GetTimeStep(timestamp));
    }

    private string GenerateCode(string externalUserId, long timeStep)
    {
        var secret = DeriveUserSecret(externalUserId);
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

    private byte[] DeriveUserSecret(string externalUserId)
    {
        using var hmac = new HMACSHA256(_masterSecret);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(externalUserId.Trim()));
    }

    private static long GetTimeStep(DateTimeOffset timestamp)
    {
        return timestamp.ToUnixTimeSeconds() / 30;
    }
}
