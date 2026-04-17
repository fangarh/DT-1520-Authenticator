namespace OtpAuth.Application.Integrations;

public interface IIntegrationClientStore
{
    Task<IntegrationClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<IntegrationClient>> ListActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}
