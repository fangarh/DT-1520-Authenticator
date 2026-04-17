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
        var currentStep = TotpCodeCalculator.GetTimeStep(timestamp, enrollment.PeriodSeconds);

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
        return GenerateCode(enrollment, TotpCodeCalculator.GetTimeStep(timestamp, enrollment.PeriodSeconds));
    }

    private static string GenerateCode(TotpEnrollmentSecret enrollment, long timeStep)
    {
        return TotpCodeCalculator.GenerateCode(
            enrollment.Secret,
            enrollment.Digits,
            enrollment.Algorithm,
            timeStep);
    }
}
