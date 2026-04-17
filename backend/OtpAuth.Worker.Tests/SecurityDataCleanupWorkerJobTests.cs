using Microsoft.Extensions.Options;
using OtpAuth.Infrastructure.Persistence;
using Xunit;

namespace OtpAuth.Worker.Tests;

public sealed class SecurityDataCleanupWorkerJobTests
{
    [Fact]
    public async Task ExecuteAsync_MapsCleanupResultToSanitizedMetrics()
    {
        var runner = new StubSecurityDataCleanupRunner(new SecurityDataCleanupResult
        {
            DeletedChallengeAttempts = 5,
            DeletedExpiredTotpUsedTimeSteps = 3,
            DeletedExpiredRevokedIntegrationAccessTokens = 2,
        });
        var job = new SecurityDataCleanupWorkerJob(
            runner,
            Options.Create(new SecurityDataCleanupWorkerJobOptions
            {
                Enabled = true,
                IntervalSeconds = 300,
            }));
        var utcNow = new DateTimeOffset(2026, 04, 15, 13, 00, 00, TimeSpan.Zero);

        var result = await job.ExecuteAsync(utcNow, CancellationToken.None);

        Assert.Equal(utcNow, runner.LastUtcNow);
        Assert.Equal("cleanup_completed", result.Summary);
        Assert.Collection(
            result.Metrics,
            metric =>
            {
                Assert.Equal("deletedChallengeAttempts", metric.Name);
                Assert.Equal(5, metric.Value);
            },
            metric =>
            {
                Assert.Equal("deletedExpiredTotpUsedTimeSteps", metric.Name);
                Assert.Equal(3, metric.Value);
            },
            metric =>
            {
                Assert.Equal("deletedExpiredRevokedIntegrationAccessTokens", metric.Name);
                Assert.Equal(2, metric.Value);
            },
            metric =>
            {
                Assert.Equal("deletedTotal", metric.Name);
                Assert.Equal(10, metric.Value);
            });
    }

    [Fact]
    public void GetInterval_RejectsNonPositiveValue()
    {
        var options = new SecurityDataCleanupWorkerJobOptions
        {
            IntervalSeconds = 0
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.GetInterval());

        Assert.Equal(
            "WorkerJobs:SecurityDataCleanup:IntervalSeconds must be a positive number of seconds.",
            exception.Message);
    }

    private sealed class StubSecurityDataCleanupRunner(SecurityDataCleanupResult result) : ISecurityDataCleanupRunner
    {
        public DateTimeOffset? LastUtcNow { get; private set; }

        public Task<SecurityDataCleanupResult> CleanupAsync(
            DateTimeOffset utcNow,
            CancellationToken cancellationToken)
        {
            LastUtcNow = utcNow;
            return Task.FromResult(result);
        }
    }
}
