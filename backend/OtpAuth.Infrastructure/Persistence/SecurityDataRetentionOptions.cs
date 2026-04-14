namespace OtpAuth.Infrastructure.Persistence;

public sealed record SecurityDataRetentionOptions
{
    public int ChallengeAttemptRetentionDays { get; init; } = 30;
}
