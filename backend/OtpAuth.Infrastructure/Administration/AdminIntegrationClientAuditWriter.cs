using System.Text.Json;
using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Administration;

public sealed class AdminIntegrationClientAuditWriter : IAdminIntegrationClientAuditWriter
{
    private readonly SecurityAuditService _securityAuditService;

    public AdminIntegrationClientAuditWriter(SecurityAuditService securityAuditService)
    {
        _securityAuditService = securityAuditService;
    }

    public Task WriteCreatedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            "admin_integration_client.created",
            "Admin created integration client.",
            adminContext,
            client,
            cancellationToken);
    }

    public Task WriteSecretRotatedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            "admin_integration_client.secret_rotated",
            "Admin rotated integration client secret.",
            adminContext,
            client,
            cancellationToken);
    }

    public Task WriteScopesChangedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            "admin_integration_client.scopes_changed",
            "Admin changed integration client scopes.",
            adminContext,
            client,
            cancellationToken);
    }

    public Task WriteDeactivatedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            "admin_integration_client.deactivated",
            "Admin deactivated integration client.",
            adminContext,
            client,
            cancellationToken);
    }

    public Task WriteReactivatedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            "admin_integration_client.reactivated",
            "Admin reactivated integration client.",
            adminContext,
            client,
            cancellationToken);
    }

    private Task RecordAsync(
        string eventType,
        string summary,
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = eventType,
                SubjectType = "integration_client",
                SubjectId = client.ClientId,
                Summary = summary,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId = adminContext.AdminUserId,
                    adminUsername = adminContext.Username,
                    action = new
                    {
                        clientId = client.ClientId,
                        tenantId = client.TenantId,
                        applicationClientId = client.ApplicationClientId,
                        status = client.Status == AdminIntegrationClientStatus.Active ? "active" : "inactive",
                        allowedScopes = client.AllowedScopes.OrderBy(static scope => scope, StringComparer.Ordinal),
                        createdUtc = client.CreatedUtc,
                    },
                }),
                Severity = "info",
                Source = "admin_api",
            },
            cancellationToken);
    }
}
