using Dapper;
using Npgsql;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class PostgresIntegrationClientStore : IIntegrationClientStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresIntegrationClientStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IntegrationClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var clientRecord = await connection.QuerySingleOrDefaultAsync<IntegrationClientRecord>(new CommandDefinition(
            """
            select
                client_id as ClientId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                client_secret_hash as ClientSecretHash
            from auth.integration_clients
            where client_id = @ClientId
              and is_active = true
            limit 1;
            """,
            new { ClientId = clientId },
            cancellationToken: cancellationToken));

        if (clientRecord is null)
        {
            return null;
        }

        var scopes = (await connection.QueryAsync<string>(new CommandDefinition(
            """
            select scope
            from auth.integration_client_scopes
            where client_id = @ClientId
            order by scope;
            """,
            new { ClientId = clientId },
            cancellationToken: cancellationToken)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var material = IntegrationClientDataMapper.ToMaterial(clientRecord);
        return new IntegrationClient
        {
            ClientId = material.ClientId,
            TenantId = material.TenantId,
            ApplicationClientId = material.ApplicationClientId,
            ClientSecretHash = material.ClientSecretHash,
            AllowedScopes = scopes,
        };
    }
}
