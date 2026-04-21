namespace OtpAuth.Application.Observability;

public sealed record DeliveryStatusMetricsSummary
{
    public required long QueuedCount { get; init; }

    public required long DeliveredCount { get; init; }

    public required long FailedCount { get; init; }

    public required long RetryingCount { get; init; }
}
