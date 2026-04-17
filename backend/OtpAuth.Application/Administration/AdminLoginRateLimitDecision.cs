namespace OtpAuth.Application.Administration;

public sealed record AdminLoginRateLimitDecision
{
    public bool IsRateLimited { get; init; }

    public int? RetryAfterSeconds { get; init; }
}
