using Microsoft.Extensions.Options;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Worker;

public sealed class PushChallengeDeliveryWorkerJob(
    PushChallengeDeliveryCoordinator coordinator,
    IOptions<PushChallengeDeliveryWorkerJobOptions> options) : IWorkerJob
{
    private readonly PushChallengeDeliveryCoordinator _coordinator = coordinator;
    private readonly PushChallengeDeliveryWorkerJobOptions _options = options.Value;

    public string Name => "push_challenge_delivery";

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

        return WorkerJobRunResult.Create(
            "push_delivery_cycle_completed",
            new WorkerJobMetricSnapshot("leased", result.LeasedCount),
            new WorkerJobMetricSnapshot("delivered", result.DeliveredCount),
            new WorkerJobMetricSnapshot("rescheduled", result.RescheduledCount),
            new WorkerJobMetricSnapshot("failed", result.FailedCount));
    }
}
