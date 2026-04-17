using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Factors;

public interface IBackupCodeVerificationRateLimiter
{
    Task<BackupCodeVerificationRateLimitDecision> EvaluateAsync(
        Challenge challenge,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}
