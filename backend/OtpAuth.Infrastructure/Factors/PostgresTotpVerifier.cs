using System.Security.Cryptography;
using System.Text;
using OtpAuth.Application.Factors;
using OtpAuth.Domain.Challenges;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresTotpVerifier : ITotpVerifier
{
    private readonly ITotpEnrollmentStore _totpEnrollmentStore;
    private readonly ITotpReplayProtector _totpReplayProtector;

    public PostgresTotpVerifier(
        ITotpEnrollmentStore totpEnrollmentStore,
        ITotpReplayProtector totpReplayProtector)
    {
        _totpEnrollmentStore = totpEnrollmentStore;
        _totpReplayProtector = totpReplayProtector;
    }

    public async Task<TotpVerificationResult> VerifyAsync(
        Challenge challenge,
        string code,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var enrollment = await _totpEnrollmentStore.GetActiveAsync(
            challenge.TenantId,
            challenge.ApplicationClientId,
            challenge.ExternalUserId,
            cancellationToken);
        if (enrollment is null)
        {
            return TotpVerificationResult.InvalidCode();
        }

        var normalizedCode = code.Trim();
        var currentStep = GetTimeStep(timestamp, enrollment.PeriodSeconds);

        for (var offset = -1; offset <= 1; offset++)
        {
            var expectedCode = GenerateCode(enrollment, currentStep + offset);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expectedCode),
                    Encoding.ASCII.GetBytes(normalizedCode)))
            {
                var matchedTimeStep = currentStep + offset;
                var reserved = await _totpReplayProtector.TryReserveAsync(
                    enrollment.EnrollmentId,
                    matchedTimeStep,
                    timestamp,
                    timestamp.AddSeconds(enrollment.PeriodSeconds * 2L),
                    cancellationToken);
                if (!reserved)
                {
                    return TotpVerificationResult.ReplayDetected(enrollment.EnrollmentId, matchedTimeStep);
                }

                await _totpEnrollmentStore.MarkUsedAsync(enrollment.EnrollmentId, timestamp, cancellationToken);
                return TotpVerificationResult.Valid(enrollment.EnrollmentId, matchedTimeStep);
            }
        }

        return TotpVerificationResult.InvalidCode();
    }

    internal static string GenerateCode(TotpEnrollmentSecret enrollment, DateTimeOffset timestamp)
    {
        return GenerateCode(enrollment, GetTimeStep(timestamp, enrollment.PeriodSeconds));
    }

    private static string GenerateCode(TotpEnrollmentSecret enrollment, long timeStep)
    {
        if (enrollment.Digits != 6)
        {
            throw new InvalidOperationException("Only 6-digit TOTP enrollments are currently supported.");
        }

        if (!string.Equals(enrollment.Algorithm, "SHA1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only SHA1 TOTP enrollments are currently supported.");
        }

        Span<byte> counterBytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(counterBytes, timeStep);

        if (BitConverter.IsLittleEndian)
        {
            counterBytes.Reverse();
        }

        using var hmac = new HMACSHA1(enrollment.Secret);
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

    private static long GetTimeStep(DateTimeOffset timestamp, int periodSeconds)
    {
        return timestamp.ToUnixTimeSeconds() / periodSeconds;
    }
}
