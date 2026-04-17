namespace OtpAuth.Infrastructure.Security;

public sealed record SecurityAuditEntry
{
    public required string EventType { get; init; }

    public required string SubjectType { get; init; }

    public string? SubjectId { get; init; }

    public required string Summary { get; init; }

    public required string PayloadJson { get; init; }

    public string Severity { get; init; } = "info";

    public string Source { get; init; } = "migration_runner";
}
