using System.Text.Json;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class SigningKeyLifecycleAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SecurityAuditService _auditService;

    public SigningKeyLifecycleAuditService(SecurityAuditService auditService)
    {
        _auditService = auditService;
    }

    public Task<SecurityAuditEvent> RecordSnapshotAsync(
        BootstrapSigningKeyLifecycleReport report,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        return _auditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "signing_key_lifecycle.snapshot",
                SubjectType = "signing_key",
                SubjectId = report.CurrentSigningKeyId,
                Summary = report.Summary,
                PayloadJson = JsonSerializer.Serialize(report, SerializerOptions),
                Severity = report.Warnings.Count > 0 ? "warning" : "info",
            },
            cancellationToken);
    }

    public Task<IReadOnlyCollection<SecurityAuditEvent>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        return _auditService.ListRecentAsync(limit, "signing_key_lifecycle.", cancellationToken);
    }
}
