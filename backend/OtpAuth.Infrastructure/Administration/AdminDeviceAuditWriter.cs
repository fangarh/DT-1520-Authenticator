using System.Text.Json;
using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Administration;

public sealed class AdminDeviceAuditWriter : IAdminDeviceAuditWriter
{
    private readonly SecurityAuditService _securityAuditService;

    public AdminDeviceAuditWriter(SecurityAuditService securityAuditService)
    {
        _securityAuditService = securityAuditService;
    }

    public Task WriteRevokedAsync(
        AdminContext adminContext,
        RegisteredDevice device,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "admin_device.revoked",
                SubjectType = "device",
                SubjectId = device.Id.ToString("D"),
                Summary = "Admin revoked user device.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId = adminContext.AdminUserId,
                    adminUsername = adminContext.Username,
                    action = new
                    {
                        deviceId = device.Id,
                        tenantId = device.TenantId,
                        applicationClientId = device.ApplicationClientId,
                        externalUserId = device.ExternalUserId,
                        platform = device.Platform.ToString().ToLowerInvariant(),
                        status = device.Status.ToString().ToLowerInvariant(),
                        isPushCapable = !string.IsNullOrWhiteSpace(device.PushToken),
                        revokedAtUtc = device.RevokedUtc,
                    },
                }),
                Severity = "warning",
                Source = "admin_api",
            },
            cancellationToken);
    }
}
