using OtpAuth.Infrastructure.Persistence;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Persistence;

public sealed class SecurityDataCleanupServiceTests
{
    [Fact]
    public async Task CleanupAsync_UsesConfiguredRetentionAndReturnsDeletedCounts()
    {
        var store = new InMemorySecurityDataRetentionStore
        {
            DeletedChallengeAttempts = 5,
            DeletedExpiredTotpUsedTimeSteps = 3,
            DeletedExpiredRevokedTokens = 2,
        };
        var service = new SecurityDataCleanupService(
            store,
            new SecurityDataRetentionOptions
            {
                ChallengeAttemptRetentionDays = 30,
            });
        var now = new DateTimeOffset(2026, 04, 14, 16, 30, 0, TimeSpan.Zero);

        var result = await service.CleanupAsync(now, CancellationToken.None);

        Assert.Equal(5, result.DeletedChallengeAttempts);
        Assert.Equal(3, result.DeletedExpiredTotpUsedTimeSteps);
        Assert.Equal(2, result.DeletedExpiredRevokedIntegrationAccessTokens);
        Assert.Equal(now.AddDays(-30), store.LastChallengeAttemptCutoffUtc);
        Assert.Equal(now, store.LastExpiredTotpUsedTimeStepsCutoffUtc);
        Assert.Equal(now, store.LastExpiredRevokedTokensCutoffUtc);
    }

    [Fact]
    public async Task CleanupAsync_Throws_WhenRetentionIsInvalid()
    {
        var service = new SecurityDataCleanupService(
            new InMemorySecurityDataRetentionStore(),
            new SecurityDataRetentionOptions
            {
                ChallengeAttemptRetentionDays = 0,
            });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CleanupAsync(DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Contains("ChallengeAttemptRetentionDays", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InMemorySecurityDataRetentionStore : ISecurityDataRetentionStore
    {
        public int DeletedChallengeAttempts { get; init; }

        public int DeletedExpiredTotpUsedTimeSteps { get; init; }

        public int DeletedExpiredRevokedTokens { get; init; }

        public DateTimeOffset? LastChallengeAttemptCutoffUtc { get; private set; }

        public DateTimeOffset? LastExpiredTotpUsedTimeStepsCutoffUtc { get; private set; }

        public DateTimeOffset? LastExpiredRevokedTokensCutoffUtc { get; private set; }

        public Task<int> DeleteExpiredTotpUsedTimeStepsAsync(DateTimeOffset utcNow, CancellationToken cancellationToken)
        {
            LastExpiredTotpUsedTimeStepsCutoffUtc = utcNow;
            return Task.FromResult(DeletedExpiredTotpUsedTimeSteps);
        }

        public Task<int> DeleteExpiredRevokedIntegrationAccessTokensAsync(DateTimeOffset utcNow, CancellationToken cancellationToken)
        {
            LastExpiredRevokedTokensCutoffUtc = utcNow;
            return Task.FromResult(DeletedExpiredRevokedTokens);
        }

        public Task<int> DeleteChallengeAttemptsOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
        {
            LastChallengeAttemptCutoffUtc = cutoffUtc;
            return Task.FromResult(DeletedChallengeAttempts);
        }
    }
}
