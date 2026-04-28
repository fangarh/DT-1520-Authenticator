using Dapper;
using Npgsql;
using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

public sealed class PostgresAdminTenantDirectoryStore : IAdminTenantDirectoryStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAdminTenantDirectoryStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<AdminTenantDirectoryTenantView>> ListTenantsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var records = await connection.QueryAsync<AdminTenantDirectoryTenantPersistenceModel>(new CommandDefinition(
            """
            select
                tenant.tenant_id as TenantId,
                tenant.display_name as DisplayName,
                tenant.slug as Slug,
                tenant.status as Status,
                count(distinct application.application_client_id)::int as ApplicationCount,
                count(distinct integration_client.client_id)::int as IntegrationClientCount,
                tenant.created_utc as CreatedUtc,
                tenant.updated_utc as UpdatedUtc
            from auth.tenants tenant
            left join auth.tenant_applications application on application.tenant_id = tenant.tenant_id
            left join auth.integration_clients integration_client on integration_client.tenant_id = tenant.tenant_id
            group by tenant.tenant_id, tenant.display_name, tenant.slug, tenant.status, tenant.created_utc, tenant.updated_utc
            order by tenant.display_name, tenant.tenant_id;
            """,
            cancellationToken: cancellationToken));

        return records
            .Select(AdminTenantDirectoryDataMapper.ToTenantView)
            .ToArray();
    }

    public async Task<AdminTenantDirectoryDetailView?> GetDirectoryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var tenant = await LoadTenantAsync(connection, tenantId, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var applications = await LoadApplicationsAsync(connection, tenantId, cancellationToken);
        var clients = await LoadIntegrationClientsAsync(connection, tenantId, cancellationToken);
        return new AdminTenantDirectoryDetailView
        {
            Tenant = tenant,
            Applications = applications,
            IntegrationClients = clients,
        };
    }

    public async Task<AdminTenantDirectoryTenantView?> CreateTenantAsync(
        AdminTenantCreateDraft draft,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<AdminTenantDirectoryTenantPersistenceModel>(new CommandDefinition(
            """
            insert into auth.tenants (
                tenant_id,
                display_name,
                normalized_display_name,
                slug,
                status,
                created_utc,
                updated_utc
            ) values (
                @TenantId,
                @DisplayName,
                upper(@DisplayName),
                @Slug,
                @Status,
                @CreatedUtc,
                @CreatedUtc
            )
            on conflict do nothing
            returning
                tenant_id as TenantId,
                display_name as DisplayName,
                slug as Slug,
                status as Status,
                0 as ApplicationCount,
                0 as IntegrationClientCount,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc;
            """,
            new
            {
                draft.TenantId,
                draft.DisplayName,
                draft.Slug,
                Status = AdminTenantDirectoryDataMapper.ToPersistenceStatus(draft.Status),
                CreatedUtc = draft.CreatedUtc.UtcDateTime,
            },
            cancellationToken: cancellationToken));

        return record is null
            ? null
            : AdminTenantDirectoryDataMapper.ToTenantView(record);
    }

    public async Task<AdminTenantDirectoryDetailView?> QuickCreateAsync(
        AdminTenantQuickCreateDraft draft,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var tenantRecord = await InsertTenantAsync(connection, transaction, draft, cancellationToken);
        if (tenantRecord is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var applicationRecord = await InsertApplicationAsync(connection, transaction, draft, cancellationToken);
        if (applicationRecord is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var clientRecord = await InsertIntegrationClientAsync(connection, transaction, draft, cancellationToken);
        if (clientRecord is null)
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

        var client = AdminIntegrationClientDataMapper.ToDomainModel(
            clientRecord,
            new Dictionary<string, string[]>
            {
                [draft.ClientId] = draft.AllowedScopes
                    .OrderBy(static scope => scope, StringComparer.Ordinal)
                    .ToArray(),
            });

        return new AdminTenantDirectoryDetailView
        {
            Tenant = AdminTenantDirectoryDataMapper.ToTenantView(tenantRecord),
            Applications = [AdminTenantDirectoryDataMapper.ToApplicationView(applicationRecord)],
            IntegrationClients = [client],
        };
    }

    private static async Task<AdminTenantDirectoryTenantView?> LoadTenantAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var record = await connection.QuerySingleOrDefaultAsync<AdminTenantDirectoryTenantPersistenceModel>(new CommandDefinition(
            """
            select
                tenant.tenant_id as TenantId,
                tenant.display_name as DisplayName,
                tenant.slug as Slug,
                tenant.status as Status,
                count(distinct application.application_client_id)::int as ApplicationCount,
                count(distinct integration_client.client_id)::int as IntegrationClientCount,
                tenant.created_utc as CreatedUtc,
                tenant.updated_utc as UpdatedUtc
            from auth.tenants tenant
            left join auth.tenant_applications application on application.tenant_id = tenant.tenant_id
            left join auth.integration_clients integration_client on integration_client.tenant_id = tenant.tenant_id
            where tenant.tenant_id = @TenantId
            group by tenant.tenant_id, tenant.display_name, tenant.slug, tenant.status, tenant.created_utc, tenant.updated_utc
            limit 1;
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        return record is null
            ? null
            : AdminTenantDirectoryDataMapper.ToTenantView(record);
    }

    private static async Task<IReadOnlyCollection<AdminTenantDirectoryApplicationView>> LoadApplicationsAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var records = await connection.QueryAsync<AdminTenantDirectoryApplicationPersistenceModel>(new CommandDefinition(
            """
            select
                application.application_client_id as ApplicationClientId,
                application.tenant_id as TenantId,
                application.display_name as DisplayName,
                application.slug as Slug,
                application.status as Status,
                count(distinct integration_client.client_id)::int as IntegrationClientCount,
                application.created_utc as CreatedUtc,
                application.updated_utc as UpdatedUtc
            from auth.tenant_applications application
            left join auth.integration_clients integration_client
                on integration_client.tenant_id = application.tenant_id
               and integration_client.application_client_id = application.application_client_id
            where application.tenant_id = @TenantId
            group by application.application_client_id, application.tenant_id, application.display_name, application.slug, application.status, application.created_utc, application.updated_utc
            order by application.display_name, application.application_client_id;
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        return records
            .Select(AdminTenantDirectoryDataMapper.ToApplicationView)
            .ToArray();
    }

    private static async Task<IReadOnlyCollection<AdminIntegrationClientView>> LoadIntegrationClientsAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
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
            new { TenantId = tenantId },
            cancellationToken: cancellationToken)))
            .ToArray();
        if (clientRecords.Length == 0)
        {
            return Array.Empty<AdminIntegrationClientView>();
        }

        var scopesByClientId = await LoadScopesByClientIdAsync(
            connection,
            clientRecords.Select(static client => client.ClientId).ToArray(),
            cancellationToken);
        return clientRecords
            .Select(client => AdminIntegrationClientDataMapper.ToDomainModel(client, scopesByClientId))
            .ToArray();
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

    private static Task<AdminTenantDirectoryTenantPersistenceModel?> InsertTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AdminTenantQuickCreateDraft draft,
        CancellationToken cancellationToken)
    {
        return connection.QuerySingleOrDefaultAsync<AdminTenantDirectoryTenantPersistenceModel>(new CommandDefinition(
            """
            insert into auth.tenants (
                tenant_id,
                display_name,
                normalized_display_name,
                slug,
                status,
                created_utc,
                updated_utc
            ) values (
                @TenantId,
                @DisplayName,
                upper(@DisplayName),
                @Slug,
                'active',
                @CreatedUtc,
                @CreatedUtc
            )
            on conflict do nothing
            returning
                tenant_id as TenantId,
                display_name as DisplayName,
                slug as Slug,
                status as Status,
                1 as ApplicationCount,
                1 as IntegrationClientCount,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc;
            """,
            new
            {
                draft.TenantId,
                DisplayName = draft.TenantDisplayName,
                Slug = draft.TenantSlug,
                CreatedUtc = draft.CreatedUtc.UtcDateTime,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task<AdminTenantDirectoryApplicationPersistenceModel?> InsertApplicationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AdminTenantQuickCreateDraft draft,
        CancellationToken cancellationToken)
    {
        return connection.QuerySingleOrDefaultAsync<AdminTenantDirectoryApplicationPersistenceModel>(new CommandDefinition(
            """
            insert into auth.tenant_applications (
                application_client_id,
                tenant_id,
                display_name,
                normalized_display_name,
                slug,
                status,
                created_utc,
                updated_utc
            ) values (
                @ApplicationClientId,
                @TenantId,
                @DisplayName,
                upper(@DisplayName),
                @Slug,
                'active',
                @CreatedUtc,
                @CreatedUtc
            )
            on conflict do nothing
            returning
                application_client_id as ApplicationClientId,
                tenant_id as TenantId,
                display_name as DisplayName,
                slug as Slug,
                status as Status,
                1 as IntegrationClientCount,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc;
            """,
            new
            {
                draft.ApplicationClientId,
                draft.TenantId,
                DisplayName = draft.ApplicationDisplayName,
                Slug = draft.ApplicationSlug,
                CreatedUtc = draft.CreatedUtc.UtcDateTime,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task<AdminIntegrationClientPersistenceModel?> InsertIntegrationClientAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AdminTenantQuickCreateDraft draft,
        CancellationToken cancellationToken)
    {
        return connection.QuerySingleOrDefaultAsync<AdminIntegrationClientPersistenceModel>(new CommandDefinition(
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
    }

    private sealed record IntegrationClientScopeRecord
    {
        public required string ClientId { get; init; }

        public required string Scope { get; init; }
    }
}
