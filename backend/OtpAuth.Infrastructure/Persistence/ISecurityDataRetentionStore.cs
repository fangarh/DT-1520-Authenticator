namespace OtpAuth.Infrastructure.Persistence;

public interface ISecurityDataRetentionStore
{
    Task<int> DeleteExpiredTotpUsedTimeStepsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);

    Task<int> DeleteExpiredRevokedIntegrationAccessTokensAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);

    Task<int> DeleteChallengeAttemptsOlderThanAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken);
}
