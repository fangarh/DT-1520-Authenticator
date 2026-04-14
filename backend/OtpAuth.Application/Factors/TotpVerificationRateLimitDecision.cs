namespace OtpAuth.Application.Factors;

public sealed record TotpVerificationRateLimitDecision
{
    public required bool IsAllowed { get; init; }

    public int? RetryAfterSeconds { get; init; }

    public static TotpVerificationRateLimitDecision Allowed() => new()
    {
        IsAllowed = true,
    };

    public static TotpVerificationRateLimitDecision Denied(int retryAfterSeconds) => new()
    {
        IsAllowed = false,
        RetryAfterSeconds = retryAfterSeconds,
    };
}
