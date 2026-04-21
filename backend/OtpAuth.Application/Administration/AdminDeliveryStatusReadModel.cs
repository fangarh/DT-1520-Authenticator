namespace OtpAuth.Application.Administration;

public enum AdminDeliveryChannel
{
    ChallengeCallback = 0,
    WebhookEvent = 1,
}

public enum AdminDeliveryStatus
{
    Queued = 0,
    Delivered = 1,
    Failed = 2,
}

public sealed record AdminDeliveryStatusListRequest
{
    public required Guid TenantId { get; init; }

    public Guid? ApplicationClientId { get; init; }

    public AdminDeliveryChannel? Channel { get; init; }

    public AdminDeliveryStatus? Status { get; init; }

    public int Limit { get; init; } = 50;
}

public sealed record AdminDeliveryStatusView
{
    public required Guid DeliveryId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required AdminDeliveryChannel Channel { get; init; }

    public required AdminDeliveryStatus Status { get; init; }

    public required string EventType { get; init; }

    public required string DeliveryDestination { get; init; }

    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public Guid? PublicationId { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset NextAttemptAtUtc { get; init; }

    public DateTimeOffset? LastAttemptAtUtc { get; init; }

    public DateTimeOffset? DeliveredAtUtc { get; init; }

    public string? LastErrorCode { get; init; }

    public bool IsRetryScheduled => Status == AdminDeliveryStatus.Queued && AttemptCount > 0;
}

public enum AdminListDeliveryStatusesErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
}

public sealed record AdminListDeliveryStatusesResult
{
    public bool IsSuccess { get; init; }

    public AdminListDeliveryStatusesErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyCollection<AdminDeliveryStatusView> Deliveries { get; init; } = Array.Empty<AdminDeliveryStatusView>();

    public static AdminListDeliveryStatusesResult Success(IReadOnlyCollection<AdminDeliveryStatusView> deliveries) => new()
    {
        IsSuccess = true,
        Deliveries = deliveries,
    };

    public static AdminListDeliveryStatusesResult Failure(
        AdminListDeliveryStatusesErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public interface IAdminDeliveryStatusStore
{
    Task<IReadOnlyCollection<AdminDeliveryStatusView>> ListRecentAsync(
        AdminDeliveryStatusListRequest request,
        CancellationToken cancellationToken);
}
