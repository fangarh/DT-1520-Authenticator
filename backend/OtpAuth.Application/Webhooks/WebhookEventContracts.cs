using OtpAuth.Application.Observability;

namespace OtpAuth.Application.Webhooks;

public static class WebhookEventTypeNames
{
    public const string ChallengeApproved = "challenge.approved";
    public const string ChallengeDenied = "challenge.denied";
    public const string ChallengeExpired = "challenge.expired";
    public const string DeviceActivated = "device.activated";
    public const string DeviceRevoked = "device.revoked";
    public const string DeviceBlocked = "device.blocked";
    public const string FactorRevoked = "factor.revoked";

    public static readonly IReadOnlyCollection<string> ChallengeTerminalEvents =
    [
        ChallengeApproved,
        ChallengeDenied,
        ChallengeExpired,
    ];

    public static readonly IReadOnlyCollection<string> All =
    [
        ChallengeApproved,
        ChallengeDenied,
        ChallengeExpired,
        DeviceActivated,
        DeviceRevoked,
        DeviceBlocked,
        FactorRevoked,
    ];
}

public static class WebhookResourceTypeNames
{
    public const string Challenge = "challenge";
    public const string Device = "device";
    public const string Factor = "factor";
}

public enum WebhookEventDeliveryStatus
{
    Queued = 0,
    Delivered = 1,
    Failed = 2,
}

public sealed record WebhookEventPublication
{
    public required Guid EventId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string EventType { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required string ResourceType { get; init; }

    public required Guid ResourceId { get; init; }

    public required string PayloadJson { get; init; }
}

public sealed record WebhookEventDelivery
{
    public required Guid DeliveryId { get; init; }

    public required Guid SubscriptionId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required Uri EndpointUrl { get; init; }

    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required string ResourceType { get; init; }

    public required Guid ResourceId { get; init; }

    public required string PayloadJson { get; init; }

    public required WebhookEventDeliveryStatus Status { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset NextAttemptUtc { get; init; }

    public DateTimeOffset? LastAttemptUtc { get; init; }

    public DateTimeOffset? DeliveredUtc { get; init; }

    public string? LastErrorCode { get; init; }

    public DateTimeOffset? LockedUntilUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

public sealed record WebhookEventDispatchRequest
{
    public required Guid DeliveryId { get; init; }

    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required Uri EndpointUrl { get; init; }

    public required string PayloadJson { get; init; }
}

public sealed record WebhookEventDispatchResult
{
    public bool IsSuccess { get; init; }

    public bool IsRetryable { get; init; }

    public string? ErrorCode { get; init; }

    public static WebhookEventDispatchResult Success() => new()
    {
        IsSuccess = true,
    };

    public static WebhookEventDispatchResult Failure(string errorCode, bool isRetryable) => new()
    {
        IsSuccess = false,
        IsRetryable = isRetryable,
        ErrorCode = errorCode,
    };
}

public sealed record WebhookSubscription
{
    public required Guid SubscriptionId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required Uri EndpointUrl { get; init; }

    public required bool IsActive { get; init; }

    public IReadOnlyCollection<string> EventTypes { get; init; } = Array.Empty<string>();

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }
}

public sealed record WebhookSubscriptionListRequest
{
    public Guid? TenantId { get; init; }

    public Guid? ApplicationClientId { get; init; }
}

public sealed record WebhookSubscriptionUpsertRequest
{
    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required Uri EndpointUrl { get; init; }

    public bool IsActive { get; init; } = true;

    public IReadOnlyCollection<string> EventTypes { get; init; } = Array.Empty<string>();
}

public interface IWebhookEventDeliveryStore
{
    Task<IReadOnlyCollection<WebhookEventDelivery>> LeaseDueAsync(
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

public interface IWebhookEventDeliveryGateway
{
    Task<WebhookEventDispatchResult> DeliverAsync(
        WebhookEventDispatchRequest request,
        CancellationToken cancellationToken);
}

public interface IWebhookSubscriptionStore
{
    Task<IReadOnlyCollection<WebhookSubscription>> ListAsync(
        WebhookSubscriptionListRequest request,
        CancellationToken cancellationToken);

    Task<WebhookSubscription> UpsertAsync(
        WebhookSubscriptionUpsertRequest request,
        CancellationToken cancellationToken);
}
