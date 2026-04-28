using System.Text.Json;
using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Administration;

public sealed class AdminDeviceOnboardingAuditWriter : IAdminDeviceOnboardingAuditWriter
{
    private readonly SecurityAuditService _securityAuditService;

    public AdminDeviceOnboardingAuditWriter(SecurityAuditService securityAuditService)
    {
        _securityAuditService = securityAuditService;
    }

    public Task WriteCreatedAsync(
        AdminContext adminContext,
        AdminDeviceOnboardingView artifact,
        CancellationToken cancellationToken)
    {
        return RecordAsync("admin_device_onboarding.created", "Admin created device onboarding artifact.", adminContext, artifact, cancellationToken);
    }

    public Task WriteRevokedAsync(
        AdminContext adminContext,
        AdminDeviceOnboardingView artifact,
        CancellationToken cancellationToken)
    {
        return RecordAsync("admin_device_onboarding.revoked", "Admin revoked device onboarding artifact.", adminContext, artifact, cancellationToken);
    }

    private Task RecordAsync(
        string eventType,
        string summary,
        AdminContext adminContext,
        AdminDeviceOnboardingView artifact,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = eventType,
                SubjectType = "device_onboarding_artifact",
                SubjectId = artifact.ActivationCodeId.ToString("D"),
                Summary = summary,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId = adminContext.AdminUserId,
                    adminUsername = adminContext.Username,
                    artifact = new
                    {
                        activationCodeId = artifact.ActivationCodeId,
                        tenantId = artifact.TenantId,
                        applicationClientId = artifact.ApplicationClientId,
                        externalUserId = artifact.ExternalUserId,
                        platform = artifact.Platform.ToString().ToLowerInvariant(),
                        status = artifact.Status.ToString().ToLowerInvariant(),
                        expiresAtUtc = artifact.ExpiresUtc,
                        consumedAtUtc = artifact.ConsumedUtc,
                        revokedAtUtc = artifact.RevokedUtc,
                    },
                }),
                Severity = artifact.Status is AdminDeviceOnboardingStatus.Revoked ? "warning" : "info",
                Source = "admin_api",
            },
            cancellationToken);
    }
}
