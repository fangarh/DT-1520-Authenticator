using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Factors;

public interface ITotpVerificationRateLimiter
{
    Task<TotpVerificationRateLimitDecision> EvaluateAsync(
        Challenge challenge,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}
