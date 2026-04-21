namespace OtpAuth.Api.Admin;

public sealed record AdminDeliveryStatusHttpResponse
{
    public required Guid DeliveryId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string Channel { get; init; }

    public required string Status { get; init; }

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

    public bool IsRetryScheduled { get; init; }
}
