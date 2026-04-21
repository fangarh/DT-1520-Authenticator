using OtpAuth.Application.Observability;
using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public enum ChallengeCallbackEventType
{
    Approved = 0,
    Denied = 1,
    Expired = 2,
}

public enum ChallengeCallbackDeliveryStatus
{
    Queued = 0,
    Delivered = 1,
    Failed = 2,
}

public sealed record ChallengeCallbackDelivery
{
    public required Guid DeliveryId { get; init; }

    public required Guid ChallengeId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required Uri CallbackUrl { get; init; }

    public required ChallengeCallbackEventType EventType { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required ChallengeCallbackDeliveryStatus Status { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset NextAttemptUtc { get; init; }

    public DateTimeOffset? LastAttemptUtc { get; init; }

    public DateTimeOffset? DeliveredUtc { get; init; }

    public string? LastErrorCode { get; init; }

    public DateTimeOffset? LockedUntilUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public static ChallengeCallbackDelivery CreateQueued(
        Challenge challenge,
        ChallengeCallbackEventType eventType,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        return new ChallengeCallbackDelivery
        {
            DeliveryId = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            TenantId = challenge.TenantId,
            ApplicationClientId = challenge.ApplicationClientId,
            CallbackUrl = challenge.CallbackUrl ?? throw new InvalidOperationException("Challenge callback URL must be present."),
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            Status = ChallengeCallbackDeliveryStatus.Queued,
            AttemptCount = 0,
            NextAttemptUtc = occurredAtUtc,
            CreatedUtc = occurredAtUtc,
        };
    }
}

public sealed record ChallengeCallbackDispatchRequest
{
    public required Guid DeliveryId { get; init; }

    public required Guid ChallengeId { get; init; }

    public required Uri CallbackUrl { get; init; }

    public required ChallengeCallbackEventType EventType { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required Challenge Challenge { get; init; }
}

public sealed record ChallengeCallbackDispatchResult
{
    public bool IsSuccess { get; init; }

    public bool IsRetryable { get; init; }

    public string? ErrorCode { get; init; }

    public static ChallengeCallbackDispatchResult Success() => new()
    {
        IsSuccess = true,
    };

    public static ChallengeCallbackDispatchResult Failure(string errorCode, bool isRetryable) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        IsRetryable = isRetryable,
    };
}

public interface IChallengeCallbackDeliveryStore
{
    Task<IReadOnlyCollection<ChallengeCallbackDelivery>> LeaseDueAsync(
        DateTimeOffset utcNow,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<DeliveryStatusMetricsSummary> GetStatusMetricsAsync(CancellationToken cancellationToken);

    Task MarkDeliveredAsync(
        Guid deliveryId,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken);

    Task RescheduleAsync(
        Guid deliveryId,
        DateTimeOffset nextAttemptUtc,
        string errorCode,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid deliveryId,
        string errorCode,
        CancellationToken cancellationToken);
}

public interface IChallengeCallbackDeliveryGateway
{
    Task<ChallengeCallbackDispatchResult> DeliverAsync(
        ChallengeCallbackDispatchRequest request,
        CancellationToken cancellationToken);
}
