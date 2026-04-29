using System.Text.Json;
using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Administration;

public sealed class AdminTenantDirectoryAuditWriter : IAdminTenantDirectoryAuditWriter
{
    private readonly SecurityAuditService _securityAuditService;

    public AdminTenantDirectoryAuditWriter(SecurityAuditService securityAuditService)
    {
        _securityAuditService = securityAuditService;
    }

    public Task WriteTenantCreatedAsync(
        AdminContext adminContext,
        AdminTenantDirectoryTenantView tenant,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "admin_tenant.created",
                SubjectType = "tenant",
                SubjectId = tenant.TenantId.ToString("D"),
                Summary = "Admin created tenant.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId = adminContext.AdminUserId,
                    adminUsername = adminContext.Username,
                    tenant = new
                    {
                        tenant.TenantId,
                        tenant.DisplayName,
                        tenant.Slug,
                        status = ToAuditStatus(tenant.Status),
                    },
                }),
                Severity = "info",
                Source = "admin_api",
            },
            cancellationToken);
    }

    public Task WriteQuickCreatedAsync(
        AdminContext adminContext,
        AdminTenantDirectoryDetailView directory,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "admin_tenant.quick_created",
                SubjectType = "tenant",
                SubjectId = directory.Tenant.TenantId.ToString("D"),
                Summary = "Admin quick-created tenant, application and initial integration client.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId = adminContext.AdminUserId,
                    adminUsername = adminContext.Username,
                    tenant = new
                    {
                        directory.Tenant.TenantId,
                        directory.Tenant.DisplayName,
                        directory.Tenant.Slug,
                        status = ToAuditStatus(directory.Tenant.Status),
                    },
                    applications = directory.Applications.Select(application => new
                    {
                        application.ApplicationClientId,
                        application.DisplayName,
                        application.Slug,
                        status = ToAuditStatus(application.Status),
                    }),
                    integrationClient = new
                    {
                        client.ClientId,
                        client.TenantId,
                        client.ApplicationClientId,
                        status = client.Status == AdminIntegrationClientStatus.Active ? "active" : "inactive",
                        allowedScopes = client.AllowedScopes.OrderBy(static scope => scope, StringComparer.Ordinal),
                    },
                }),
                Severity = "info",
                Source = "admin_api",
            },
            cancellationToken);
    }

    private static string ToAuditStatus(AdminTenantDirectoryStatus status)
    {
        return status switch
        {
            AdminTenantDirectoryStatus.Active => "active",
            AdminTenantDirectoryStatus.Disabled => "disabled",
            AdminTenantDirectoryStatus.Archived => "archived",
            AdminTenantDirectoryStatus.Test => "test",
            _ => throw new InvalidOperationException($"Unsupported tenant directory status '{status}'."),
        };
    }
}
