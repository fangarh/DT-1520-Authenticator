using Dapper;
using Npgsql;
using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

public sealed class PostgresAdminIntegrationClientStore : IAdminIntegrationClientStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAdminIntegrationClientStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<AdminIntegrationClientView>> ListByTenantAsync(
        AdminIntegrationClientListRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var clientRecords = (await connection.QueryAsync<AdminIntegrationClientPersistenceModel>(new CommandDefinition(
            """
            select
                client_id as ClientId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                is_active as IsActive,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc
            from auth.integration_clients
            where tenant_id = @TenantId
            order by client_id;
            """,
            new { request.TenantId },
            cancellationToken: cancellationToken)))
            .ToArray();
        if (clientRecords.Length == 0)
        {
            return Array.Empty<AdminIntegrationClientView>();
        }

        var clientIds = clientRecords
            .Select(static client => client.ClientId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var scopesByClientId = await LoadScopesByClientIdAsync(connection, clientIds, cancellationToken);

        return clientRecords
            .Select(client => AdminIntegrationClientDataMapper.ToDomainModel(client, scopesByClientId))
            .ToArray();
    }

    public async Task<AdminIntegrationClientView?> GetByTenantAndClientIdAsync(
        Guid tenantId,
        string clientId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var clientRecord = await connection.QuerySingleOrDefaultAsync<AdminIntegrationClientPersistenceModel>(new CommandDefinition(
            """
            select
                client_id as ClientId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                is_active as IsActive,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc
            from auth.integration_clients
            where tenant_id = @TenantId and client_id = @ClientId
            limit 1;
            """,
            new { TenantId = tenantId, ClientId = clientId },
            cancellationToken: cancellationToken));
        if (clientRecord is null)
        {
            return null;
        }

        var scopesByClientId = await LoadScopesByClientIdAsync(connection, [clientId], cancellationToken);
        return AdminIntegrationClientDataMapper.ToDomainModel(clientRecord, scopesByClientId);
    }

    public async Task<AdminIntegrationClientView?> CreateAsync(
        AdminIntegrationClientCreateDraft draft,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var createdRecord = await connection.QuerySingleOrDefaultAsync<AdminIntegrationClientPersistenceModel>(new CommandDefinition(
            """
            insert into auth.integration_clients (
                client_id,
                tenant_id,
                application_client_id,
                client_secret_hash,
                is_active,
                created_utc,
                updated_utc,
                last_secret_rotated_utc,
                last_auth_state_changed_utc
            ) values (
                @ClientId,
                @TenantId,
                @ApplicationClientId,
                @ClientSecretHash,
                true,
                @CreatedUtc,
                @CreatedUtc,
                @CreatedUtc,
                @CreatedUtc
            )
            on conflict (client_id) do nothing
            returning
                client_id as ClientId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                is_active as IsActive,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc;
            """,
            new
            {
                draft.ClientId,
                draft.TenantId,
                draft.ApplicationClientId,
                draft.ClientSecretHash,
                CreatedUtc = draft.CreatedUtc.UtcDateTime,
            },
            transaction,
            cancellationToken: cancellationToken));
        if (createdRecord is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var scopeRows = draft.AllowedScopes
            .Distinct(StringComparer.Ordinal)
            .Select(scope => new { draft.ClientId, Scope = scope })
            .ToArray();
        if (scopeRows.Length > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                insert into auth.integration_client_scopes (client_id, scope)
                values (@ClientId, @Scope);
                """,
                scopeRows,
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);

        return AdminIntegrationClientDataMapper.ToDomainModel(
            createdRecord,
            new Dictionary<string, string[]>
            {
                [draft.ClientId] = draft.AllowedScopes
                    .OrderBy(static scope => scope, StringComparer.Ordinal)
                    .ToArray(),
            });
    }

    public async Task<AdminIntegrationClientView?> RotateSecretAsync(
        Guid tenantId,
        string clientId,
        string clientSecretHash,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var updatedRecord = await connection.QuerySingleOrDefaultAsync<AdminIntegrationClientPersistenceModel>(new CommandDefinition(
            """
            update auth.integration_clients
            set client_secret_hash = @ClientSecretHash,
                updated_utc = @ChangedAtUtc,
                last_secret_rotated_utc = @ChangedAtUtc,
                last_auth_state_changed_utc = @ChangedAtUtc
            where tenant_id = @TenantId and client_id = @ClientId
            returning
                client_id as ClientId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                is_active as IsActive,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc;
            """,
            new
            {
                TenantId = tenantId,
                ClientId = clientId,
                ClientSecretHash = clientSecretHash,
                ChangedAtUtc = changedAtUtc.UtcDateTime,
            },
            cancellationToken: cancellationToken));
        if (updatedRecord is null)
        {
            return null;
        }

        var scopesByClientId = await LoadScopesByClientIdAsync(connection, [clientId], cancellationToken);
        return AdminIntegrationClientDataMapper.ToDomainModel(updatedRecord, scopesByClientId);
    }

    public async Task<AdminIntegrationClientView?> UpdateScopesAsync(
        Guid tenantId,
        string clientId,
        IReadOnlyCollection<string> allowedScopes,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existingRecord = await connection.QuerySingleOrDefaultAsync<AdminIntegrationClientPersistenceModel>(new CommandDefinition(
            """
            select
                client_id as ClientId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                is_active as IsActive,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc
            from auth.integration_clients
            where tenant_id = @TenantId and client_id = @ClientId
            for update;
            """,
            new { TenantId = tenantId, ClientId = clientId },
            transaction,
            cancellationToken: cancellationToken));
        if (existingRecord is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            delete from auth.integration_client_scopes
            where client_id = @ClientId;
            """,
            new { ClientId = clientId },
            transaction,
            cancellationToken: cancellationToken));

        var scopeRows = allowedScopes
            .Distinct(StringComparer.Ordinal)
            .Select(scope => new { ClientId = clientId, Scope = scope })
            .ToArray();
        if (scopeRows.Length > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                insert into auth.integration_client_scopes (client_id, scope)
                values (@ClientId, @Scope);
                """,
                scopeRows,
                transaction,
                cancellationToken: cancellationToken));
        }

        var updatedRecord = await connection.QuerySingleAsync<AdminIntegrationClientPersistenceModel>(new CommandDefinition(
            """
            update auth.integration_clients
            set updated_utc = @ChangedAtUtc,
                last_auth_state_changed_utc = @ChangedAtUtc
            where tenant_id = @TenantId and client_id = @ClientId
            returning
                client_id as ClientId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                is_active as IsActive,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc;
            """,
            new
            {
                TenantId = tenantId,
                ClientId = clientId,
                ChangedAtUtc = changedAtUtc.UtcDateTime,
            },
            transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);

        return AdminIntegrationClientDataMapper.ToDomainModel(
            updatedRecord,
            new Dictionary<string, string[]>
            {
                [clientId] = allowedScopes
                    .OrderBy(static scope => scope, StringComparer.Ordinal)
                    .ToArray(),
            });
    }

    public async Task<AdminIntegrationClientView?> SetIsActiveAsync(
        Guid tenantId,
        string clientId,
        bool isActive,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var updatedRecord = await connection.QuerySingleOrDefaultAsync<AdminIntegrationClientPersistenceModel>(new CommandDefinition(
            """
            update auth.integration_clients
            set is_active = @IsActive,
                updated_utc = @ChangedAtUtc,
                last_auth_state_changed_utc = @ChangedAtUtc
            where tenant_id = @TenantId and client_id = @ClientId
            returning
                client_id as ClientId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                is_active as IsActive,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc;
            """,
            new
            {
                TenantId = tenantId,
                ClientId = clientId,
                IsActive = isActive,
                ChangedAtUtc = changedAtUtc.UtcDateTime,
            },
            cancellationToken: cancellationToken));
        if (updatedRecord is null)
        {
            return null;
        }

        var scopesByClientId = await LoadScopesByClientIdAsync(connection, [clientId], cancellationToken);
        return AdminIntegrationClientDataMapper.ToDomainModel(updatedRecord, scopesByClientId);
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
