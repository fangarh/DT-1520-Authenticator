namespace OtpAuth.Application.Administration;

public interface IAdminTenantDirectoryAuditWriter
{
    Task WriteTenantCreatedAsync(
        AdminContext adminContext,
        AdminTenantDirectoryTenantView tenant,
        CancellationToken cancellationToken);

    Task WriteQuickCreatedAsync(
        AdminContext adminContext,
        AdminTenantDirectoryDetailView directory,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken);
}
