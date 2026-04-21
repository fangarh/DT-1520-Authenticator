using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Worker;

public sealed class ChallengeCallbackDeliveryWorkerJob(
    ChallengeCallbackDeliveryCoordinator coordinator,
    IChallengeCallbackDeliveryStore store,
    IOptions<ChallengeCallbackDeliveryWorkerJobOptions> options,
    ILogger<ChallengeCallbackDeliveryWorkerJob> logger) : IWorkerJob
{
    private readonly ChallengeCallbackDeliveryCoordinator _coordinator = coordinator;
    private readonly IChallengeCallbackDeliveryStore _store = store;
    private readonly ChallengeCallbackDeliveryWorkerJobOptions _options = options.Value;
    private readonly ILogger<ChallengeCallbackDeliveryWorkerJob> _logger = logger;

    public string Name => "challenge_callback_delivery";

    public bool IsEnabled => _options.Enabled;

    public TimeSpan GetInterval() => _options.GetInterval();

    public async Task<WorkerJobRunResult> ExecuteAsync(DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        var result = await _coordinator.DeliverDueAsync(
            utcNow,
            _options.GetBatchSize(),
            _options.GetLeaseDuration(),
            _options.GetRetryDelay(),
            _options.GetMaxAttempts(),
            cancellationToken);
        var statusMetrics = await _store.GetStatusMetricsAsync(cancellationToken);

        _logger.LogInformation(
            "Delivery metrics baseline updated for {Channel}. queued={QueuedCount} retrying={RetryingCount} delivered={DeliveredCount} failed={FailedCount} cycleLeased={CycleLeasedCount} cycleDelivered={CycleDeliveredCount} cycleRescheduled={CycleRescheduledCount} cycleFailed={CycleFailedCount}",
            "challenge_callback",
            statusMetrics.QueuedCount,
            statusMetrics.RetryingCount,
            statusMetrics.DeliveredCount,
            statusMetrics.FailedCount,
            result.LeasedCount,
            result.DeliveredCount,
            result.RescheduledCount,
            result.FailedCount);

        return WorkerJobRunResult.Create(
            "challenge_callback_delivery_cycle_completed",
            new WorkerJobMetricSnapshot("leased", result.LeasedCount),
            new WorkerJobMetricSnapshot("delivered", result.DeliveredCount),
            new WorkerJobMetricSnapshot("rescheduled", result.RescheduledCount),
            new WorkerJobMetricSnapshot("failed", result.FailedCount),
            new WorkerJobMetricSnapshot("queued", statusMetrics.QueuedCount),
            new WorkerJobMetricSnapshot("retrying", statusMetrics.RetryingCount),
            new WorkerJobMetricSnapshot("deliveredTotal", statusMetrics.DeliveredCount),
            new WorkerJobMetricSnapshot("failedTotal", statusMetrics.FailedCount));
    }
}
