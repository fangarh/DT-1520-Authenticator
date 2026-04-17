namespace OtpAuth.Application.Factors;

public sealed record BackupCodeVerificationRateLimitDecision
{
    public required bool IsAllowed { get; init; }

    public int? RetryAfterSeconds { get; init; }

    public static BackupCodeVerificationRateLimitDecision Allowed() => new()
    {
        IsAllowed = true,
    };

    public static BackupCodeVerificationRateLimitDecision Denied(int retryAfterSeconds) => new()
    {
        IsAllowed = false,
        RetryAfterSeconds = retryAfterSeconds,
    };
}
