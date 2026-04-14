namespace OtpAuth.Infrastructure.Persistence;

public sealed record SecurityDataCleanupResult
{
    public required int DeletedChallengeAttempts { get; init; }

    public required int DeletedExpiredTotpUsedTimeSteps { get; init; }

    public required int DeletedExpiredRevokedIntegrationAccessTokens { get; init; }
}
