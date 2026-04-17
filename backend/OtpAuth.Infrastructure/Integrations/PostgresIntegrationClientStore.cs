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
                client_secret_hash as ClientSecretHash,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc
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

        var scopesByClientId = await LoadScopesByClientIdAsync(connection, [clientId], cancellationToken);
        return Map(clientRecord, scopesByClientId);
    }

    public async Task<IReadOnlyCollection<IntegrationClient>> ListActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var clientRecords = (await connection.QueryAsync<IntegrationClientRecord>(new CommandDefinition(
            """
            select
                client_id as ClientId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                client_secret_hash as ClientSecretHash,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc
            from auth.integration_clients
            where tenant_id = @TenantId
              and is_active = true
            order by client_id;
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken)))
            .ToArray();
        if (clientRecords.Length == 0)
        {
            return Array.Empty<IntegrationClient>();
        }

        var clientIds = clientRecords
            .Select(static client => client.ClientId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var scopesByClientId = await LoadScopesByClientIdAsync(connection, clientIds, cancellationToken);

        return clientRecords
            .Select(client => Map(client, scopesByClientId))
            .ToArray();
    }

    private static IntegrationClient Map(
        IntegrationClientRecord clientRecord,
        IReadOnlyDictionary<string, string[]> scopesByClientId)
    {
        var material = IntegrationClientDataMapper.ToMaterial(clientRecord);
        return new IntegrationClient
        {
            ClientId = material.ClientId,
            TenantId = material.TenantId,
            ApplicationClientId = material.ApplicationClientId,
            ClientSecretHash = material.ClientSecretHash,
            LastSecretRotatedUtc = material.LastSecretRotatedUtc,
            LastAuthStateChangedUtc = material.LastAuthStateChangedUtc,
            AllowedScopes = scopesByClientId.TryGetValue(material.ClientId, out var scopes)
                ? scopes
                : [],
        };
    }

    private static async Task<IReadOnlyDictionary<string, string[]>> LoadScopesByClientIdAsync(
        NpgsqlConnection connection,
        string[] clientIds,
        CancellationToken cancellationToken)
    {
        if (clientIds.Length == 0)
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal);
        }

        var scopeRecords = await connection.QueryAsync<IntegrationClientScopeRecord>(new CommandDefinition(
            """
            select
                client_id as ClientId,
                scope as Scope
            from auth.integration_client_scopes
            where client_id = any(@ClientIds)
            order by client_id, scope;
            """,
            new { ClientIds = clientIds },
            cancellationToken: cancellationToken));

        return scopeRecords
            .GroupBy(static record => record.ClientId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .Select(static record => record.Scope)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private sealed record IntegrationClientScopeRecord
    {
        public required string ClientId { get; init; }

        public required string Scope { get; init; }
    }
}
