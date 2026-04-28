namespace OtpAuth.Application.Administration;

public interface IAdminIntegrationClientAuditWriter
{
    Task WriteCreatedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken);

    Task WriteSecretRotatedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken);

    Task WriteScopesChangedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken);

    Task WriteDeactivatedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken);

    Task WriteReactivatedAsync(
        AdminContext adminContext,
        AdminIntegrationClientView client,
        CancellationToken cancellationToken);
}
