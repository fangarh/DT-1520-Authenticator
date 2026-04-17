namespace OtpAuth.Infrastructure.Security;

public sealed class SecurityAuditService
{
    private readonly ISecurityAuditStore _auditStore;

    public SecurityAuditService(ISecurityAuditStore auditStore)
    {
        _auditStore = auditStore;
    }

    public async Task<SecurityAuditEvent> RecordAsync(
        SecurityAuditEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        cancellationToken.ThrowIfCancellationRequested();

        var auditEvent = new SecurityAuditEvent
        {
            EventId = Guid.NewGuid(),
            EventType = RequireNonEmpty(entry.EventType, nameof(entry.EventType)),
            SubjectType = RequireNonEmpty(entry.SubjectType, nameof(entry.SubjectType)),
            SubjectId = NormalizeOptional(entry.SubjectId),
            Summary = RequireNonEmpty(entry.Summary, nameof(entry.Summary)),
            PayloadJson = RequireNonEmpty(entry.PayloadJson, nameof(entry.PayloadJson)),
            Severity = NormalizeOrDefault(entry.Severity, "info"),
            Source = NormalizeOrDefault(entry.Source, "migration_runner"),
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        await _auditStore.AppendAsync(auditEvent, cancellationToken);
        return auditEvent;
    }

    public Task<IReadOnlyCollection<SecurityAuditEvent>> ListRecentAsync(
        int limit,
        string? eventTypePrefix,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedLimit = limit <= 0
            ? 10
            : Math.Min(limit, 100);
        var normalizedPrefix = NormalizeOptional(eventTypePrefix);
        return _auditStore.ListRecentAsync(normalizedLimit, normalizedPrefix, cancellationToken);
    }

    private static string RequireNonEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{paramName} must be provided.");
        }

        return value.Trim();
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
