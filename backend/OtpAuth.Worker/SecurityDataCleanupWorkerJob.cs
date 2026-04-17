using Microsoft.Extensions.Options;

namespace OtpAuth.Worker;

public sealed class SecurityDataCleanupWorkerJob(
    ISecurityDataCleanupRunner cleanupRunner,
    IOptions<SecurityDataCleanupWorkerJobOptions> options) : IWorkerJob
{
    private readonly ISecurityDataCleanupRunner _cleanupRunner = cleanupRunner;
    private readonly SecurityDataCleanupWorkerJobOptions _options = options.Value;

    public string Name => "security_data_cleanup";

    public bool IsEnabled => _options.Enabled;

    public TimeSpan GetInterval() => _options.GetInterval();

    public async Task<WorkerJobRunResult> ExecuteAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var result = await _cleanupRunner.CleanupAsync(utcNow, cancellationToken);
        var deletedTotal =
            result.DeletedChallengeAttempts +
            result.DeletedExpiredTotpUsedTimeSteps +
            result.DeletedExpiredRevokedIntegrationAccessTokens;

        return WorkerJobRunResult.Create(
            "cleanup_completed",
            new WorkerJobMetricSnapshot("deletedChallengeAttempts", result.DeletedChallengeAttempts),
            new WorkerJobMetricSnapshot("deletedExpiredTotpUsedTimeSteps", result.DeletedExpiredTotpUsedTimeSteps),
            new WorkerJobMetricSnapshot(
                "deletedExpiredRevokedIntegrationAccessTokens",
                result.DeletedExpiredRevokedIntegrationAccessTokens),
            new WorkerJobMetricSnapshot("deletedTotal", deletedTotal));
    }
}
