using System.Text.Json;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Factors;

public sealed class TotpProtectionKeyLifecycleAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SecurityAuditService _auditService;

    public TotpProtectionKeyLifecycleAuditService(SecurityAuditService auditService)
    {
        _auditService = auditService;
    }

    public Task<SecurityAuditEvent> RecordSnapshotAsync(
        TotpProtectionKeyLifecycleReport report,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        return _auditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "totp_protection_key_lifecycle.snapshot",
                SubjectType = "totp_protection_key",
                SubjectId = report.CurrentKeyVersion.ToString(),
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
        return _auditService.ListRecentAsync(limit, "totp_protection_key_lifecycle.", cancellationToken);
    }
}
