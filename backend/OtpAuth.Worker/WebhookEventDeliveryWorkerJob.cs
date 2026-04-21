using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpAuth.Application.Webhooks;

namespace OtpAuth.Worker;

public sealed class WebhookEventDeliveryWorkerJob(
    WebhookEventDeliveryCoordinator coordinator,
    IWebhookEventDeliveryStore store,
    IOptions<WebhookEventDeliveryWorkerJobOptions> options,
    ILogger<WebhookEventDeliveryWorkerJob> logger) : IWorkerJob
{
    private readonly WebhookEventDeliveryCoordinator _coordinator = coordinator;
    private readonly IWebhookEventDeliveryStore _store = store;
    private readonly WebhookEventDeliveryWorkerJobOptions _options = options.Value;
    private readonly ILogger<WebhookEventDeliveryWorkerJob> _logger = logger;

    public string Name => "webhook_event_delivery";

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
            "webhook_event",
            statusMetrics.QueuedCount,
            statusMetrics.RetryingCount,
            statusMetrics.DeliveredCount,
            statusMetrics.FailedCount,
            result.LeasedCount,
            result.DeliveredCount,
            result.RescheduledCount,
            result.FailedCount);

        return WorkerJobRunResult.Create(
            "webhook_event_delivery_cycle_completed",
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
