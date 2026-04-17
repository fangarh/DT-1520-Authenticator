namespace OtpAuth.Application.Challenges;

public enum PushChallengeDeliveryStatus
{
    Queued = 0,
    Delivered = 1,
    Failed = 2,
}

public sealed record PushChallengeDelivery
{
    public required Guid DeliveryId { get; init; }

    public required Guid ChallengeId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required Guid TargetDeviceId { get; init; }

    public required PushChallengeDeliveryStatus Status { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset NextAttemptUtc { get; init; }

    public DateTimeOffset? LastAttemptUtc { get; init; }

    public DateTimeOffset? DeliveredUtc { get; init; }

    public string? LastErrorCode { get; init; }

    public DateTimeOffset? LockedUntilUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public static PushChallengeDelivery CreateQueued(
        Guid challengeId,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        Guid targetDeviceId,
        DateTimeOffset queuedAtUtc)
    {
        return new PushChallengeDelivery
        {
            DeliveryId = Guid.NewGuid(),
            ChallengeId = challengeId,
            TenantId = tenantId,
            ApplicationClientId = applicationClientId,
            ExternalUserId = externalUserId,
            TargetDeviceId = targetDeviceId,
            Status = PushChallengeDeliveryStatus.Queued,
            AttemptCount = 0,
            NextAttemptUtc = queuedAtUtc,
            CreatedUtc = queuedAtUtc,
        };
    }
}

public sealed record PushChallengeDispatchRequest
{
    public required Guid DeliveryId { get; init; }

    public required Guid ChallengeId { get; init; }

    public required Guid TargetDeviceId { get; init; }

    public required string PushToken { get; init; }

    public required string ExternalUserId { get; init; }

    public required string OperationType { get; init; }

    public string? OperationDisplayName { get; init; }

    public string? CorrelationId { get; init; }
}

public sealed record PushChallengeDispatchResult
{
    public bool IsSuccess { get; init; }

    public bool IsRetryable { get; init; }

    public string? ProviderMessageId { get; init; }

    public string? ErrorCode { get; init; }

    public static PushChallengeDispatchResult Success(string? providerMessageId = null) => new()
    {
        IsSuccess = true,
        ProviderMessageId = providerMessageId,
    };

    public static PushChallengeDispatchResult Failure(string errorCode, bool isRetryable) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        IsRetryable = isRetryable,
    };
}

public interface IPushChallengeDeliveryStore
{
    Task<IReadOnlyCollection<PushChallengeDelivery>> LeaseDueAsync(
        DateTimeOffset utcNow,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task MarkDeliveredAsync(
        Guid deliveryId,
        DateTimeOffset deliveredAtUtc,
        string? providerMessageId,
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

public interface IPushChallengeDeliveryGateway
{
    Task<PushChallengeDispatchResult> DeliverAsync(
        PushChallengeDispatchRequest request,
        CancellationToken cancellationToken);
}
