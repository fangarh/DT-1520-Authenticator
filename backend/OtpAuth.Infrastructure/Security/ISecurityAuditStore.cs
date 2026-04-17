namespace OtpAuth.Infrastructure.Security;

public interface ISecurityAuditStore
{
    Task AppendAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SecurityAuditEvent>> ListRecentAsync(
        int limit,
        string? eventTypePrefix,
        CancellationToken cancellationToken);
}
