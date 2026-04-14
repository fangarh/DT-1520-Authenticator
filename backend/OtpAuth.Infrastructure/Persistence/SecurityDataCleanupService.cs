namespace OtpAuth.Infrastructure.Persistence;

public sealed class SecurityDataCleanupService
{
    private readonly ISecurityDataRetentionStore _retentionStore;
    private readonly SecurityDataRetentionOptions _options;

    public SecurityDataCleanupService(
        ISecurityDataRetentionStore retentionStore,
        SecurityDataRetentionOptions options)
    {
        _retentionStore = retentionStore;
        _options = options;
    }

    public async Task<SecurityDataCleanupResult> CleanupAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        if (_options.ChallengeAttemptRetentionDays <= 0)
        {
            throw new InvalidOperationException("SecurityRetention:ChallengeAttemptRetentionDays must be greater than zero.");
        }

        var challengeAttemptCutoffUtc = utcNow.AddDays(-_options.ChallengeAttemptRetentionDays);

        var deletedChallengeAttempts = await _retentionStore.DeleteChallengeAttemptsOlderThanAsync(
            challengeAttemptCutoffUtc,
            cancellationToken);
        var deletedExpiredTotpUsedTimeSteps = await _retentionStore.DeleteExpiredTotpUsedTimeStepsAsync(
            utcNow,
            cancellationToken);
        var deletedExpiredRevokedIntegrationAccessTokens = await _retentionStore.DeleteExpiredRevokedIntegrationAccessTokensAsync(
            utcNow,
            cancellationToken);

        return new SecurityDataCleanupResult
        {
            DeletedChallengeAttempts = deletedChallengeAttempts,
            DeletedExpiredTotpUsedTimeSteps = deletedExpiredTotpUsedTimeSteps,
            DeletedExpiredRevokedIntegrationAccessTokens = deletedExpiredRevokedIntegrationAccessTokens,
        };
    }
}
