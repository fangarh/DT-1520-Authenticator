using System.Text.Json;
using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Devices;

public sealed class DeviceLifecycleAuditWriter : IDeviceLifecycleAuditWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SecurityAuditService _securityAuditService;

    public DeviceLifecycleAuditWriter(SecurityAuditService securityAuditService)
    {
        _securityAuditService = securityAuditService;
    }

    public Task WriteActivatedAsync(RegisteredDevice device, CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            CreateEntry(
                "device.activated",
                device,
                $"device_id={device.Id}; operation=activated; status={device.Status.ToString().ToLowerInvariant()}",
                new
                {
                    deviceId = device.Id,
                    tenantId = device.TenantId,
                    applicationClientId = device.ApplicationClientId,
                    externalUserId = device.ExternalUserId,
                    platform = device.Platform.ToString().ToLowerInvariant(),
                    status = device.Status.ToString().ToLowerInvariant(),
                    activatedAtUtc = device.ActivatedUtc,
                }),
            cancellationToken);
    }

    public Task WriteTokenRefreshedAsync(RegisteredDevice device, CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            CreateEntry(
                "device.token_refreshed",
                device,
                $"device_id={device.Id}; operation=token_refreshed; status={device.Status.ToString().ToLowerInvariant()}",
                new
                {
                    deviceId = device.Id,
                    tenantId = device.TenantId,
                    applicationClientId = device.ApplicationClientId,
                    status = device.Status.ToString().ToLowerInvariant(),
                    lastSeenUtc = device.LastSeenUtc,
                }),
            cancellationToken);
    }

    public Task WriteRefreshReuseDetectedAsync(RegisteredDevice device, string tokenState, CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            CreateEntry(
                "device.refresh_reuse_detected",
                device,
                $"device_id={device.Id}; operation=refresh_reuse_detected; token_state={tokenState}",
                new
                {
                    deviceId = device.Id,
                    tenantId = device.TenantId,
                    applicationClientId = device.ApplicationClientId,
                    tokenState,
                    blockedAtUtc = device.BlockedUtc,
                },
                severity: "warning"),
            cancellationToken);
    }

    public Task WriteRevokedAsync(RegisteredDevice device, bool stateChanged, CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            CreateEntry(
                "device.revoked",
                device,
                $"device_id={device.Id}; operation=revoked; outcome={(stateChanged ? "applied" : "already_applied")}",
                new
                {
                    deviceId = device.Id,
                    tenantId = device.TenantId,
                    applicationClientId = device.ApplicationClientId,
                    stateChanged,
                    status = device.Status.ToString().ToLowerInvariant(),
                    revokedAtUtc = device.RevokedUtc,
                },
                severity: stateChanged ? "info" : "warning"),
            cancellationToken);
    }

    public Task WriteBlockedAsync(RegisteredDevice device, string reason, bool stateChanged, CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            CreateEntry(
                "device.blocked",
                device,
                $"device_id={device.Id}; operation=blocked; reason={reason}; outcome={(stateChanged ? "applied" : "already_applied")}",
                new
                {
                    deviceId = device.Id,
                    tenantId = device.TenantId,
                    applicationClientId = device.ApplicationClientId,
                    reason,
                    stateChanged,
                    blockedAtUtc = device.BlockedUtc,
                },
                severity: "warning"),
            cancellationToken);
    }

    private static SecurityAuditEntry CreateEntry(
        string eventType,
        RegisteredDevice device,
        string summary,
        object payload,
        string severity = "info")
    {
        return new SecurityAuditEntry
        {
            EventType = eventType,
            SubjectType = "device",
            SubjectId = device.Id.ToString(),
            Summary = summary,
            PayloadJson = JsonSerializer.Serialize(payload, SerializerOptions),
            Severity = severity,
            Source = "device_registry",
        };
    }
}
