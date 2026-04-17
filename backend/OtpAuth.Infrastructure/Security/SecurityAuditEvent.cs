namespace OtpAuth.Infrastructure.Security;

public sealed record SecurityAuditEvent
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required string SubjectType { get; init; }

    public string? SubjectId { get; init; }

    public required string Summary { get; init; }

    public required string PayloadJson { get; init; }

    public required string Severity { get; init; }

    public required string Source { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}
