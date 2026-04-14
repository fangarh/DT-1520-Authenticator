using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Factors;

public interface ITotpVerifier
{
    Task<TotpVerificationResult> VerifyAsync(
        Challenge challenge,
        string code,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}
