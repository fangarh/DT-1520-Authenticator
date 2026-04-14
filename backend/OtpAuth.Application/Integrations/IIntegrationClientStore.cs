namespace OtpAuth.Application.Integrations;

public interface IIntegrationClientStore
{
    Task<IntegrationClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken);
}
