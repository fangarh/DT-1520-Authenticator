using OtpAuth.Application.Integrations;

namespace OtpAuth.Infrastructure.Integrations;

[Obsolete("Legacy bootstrap in-memory store kept only for transitional/local scenarios. Runtime uses PostgresIntegrationClientStore.")]
public sealed class BootstrapIntegrationClientStore : IIntegrationClientStore
{
    private readonly IReadOnlyDictionary<string, IntegrationClient> _clients;

    public BootstrapIntegrationClientStore(IEnumerable<IntegrationClient> clients)
    {
        _clients = clients.ToDictionary(client => client.ClientId, StringComparer.Ordinal);
    }

    public Task<IntegrationClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _clients.TryGetValue(clientId, out var client);
        return Task.FromResult(client);
    }

    public Task<IReadOnlyCollection<IntegrationClient>> ListActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var clients = _clients.Values
            .Where(client => client.TenantId == tenantId)
            .OrderBy(client => client.ClientId, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<IntegrationClient>>(clients);
    }

    public static BootstrapIntegrationClientStore CreateFromOptions(
        BootstrapOAuthOptions options,
        IClientSecretHasher hasher)
    {
        var clients = new List<IntegrationClient>();

        foreach (var client in options.Clients)
        {
            var clientSecret = Environment.GetEnvironmentVariable(client.ClientSecretEnvVarName);
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                continue;
            }

            clients.Add(new IntegrationClient
            {
                ClientId = client.ClientId,
                TenantId = client.TenantId,
                ApplicationClientId = client.ApplicationClientId,
                ClientSecretHash = hasher.Hash(clientSecret),
                AllowedScopes = client.AllowedScopes
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
            });
        }

        return new BootstrapIntegrationClientStore(clients);
    }
}
