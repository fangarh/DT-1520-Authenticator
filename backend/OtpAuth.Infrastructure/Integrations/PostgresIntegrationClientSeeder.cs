using Dapper;
using Npgsql;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class PostgresIntegrationClientSeeder
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresIntegrationClientSeeder(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task UpsertAsync(
        IReadOnlyCollection<BootstrapIntegrationClientSeedMaterial> clients,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clients);

        if (clients.Count == 0)
        {
            return;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var client in clients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await connection.ExecuteAsync(new CommandDefinition(
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
                    timezone('utc', now()),
                    timezone('utc', now()),
                    timezone('utc', now()),
                    timezone('utc', now())
                )
                on conflict (client_id) do update
                set tenant_id = excluded.tenant_id,
                    application_client_id = excluded.application_client_id,
                    client_secret_hash = excluded.client_secret_hash,
                    is_active = true,
                    updated_utc = timezone('utc', now()),
                    last_secret_rotated_utc = case
                        when auth.integration_clients.client_secret_hash <> excluded.client_secret_hash
                            then timezone('utc', now())
                        else auth.integration_clients.last_secret_rotated_utc
                    end,
                    last_auth_state_changed_utc = case
                        when auth.integration_clients.tenant_id <> excluded.tenant_id
                            or auth.integration_clients.application_client_id <> excluded.application_client_id
                            or auth.integration_clients.client_secret_hash <> excluded.client_secret_hash
                            or auth.integration_clients.is_active <> true
                            then timezone('utc', now())
                        else auth.integration_clients.last_auth_state_changed_utc
                    end;
                """,
                new
                {
                    client.ClientId,
                    client.TenantId,
                    client.ApplicationClientId,
                    client.ClientSecretHash,
                },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "delete from auth.integration_client_scopes where client_id = @ClientId;",
                new { client.ClientId },
                transaction,
                cancellationToken: cancellationToken));

            var scopes = client.AllowedScopes
                .Distinct(StringComparer.Ordinal)
                .Select(scope => new { client.ClientId, Scope = scope })
                .ToArray();

            if (scopes.Length > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "insert into auth.integration_client_scopes (client_id, scope) values (@ClientId, @Scope);",
                    scopes,
                    transaction,
                    cancellationToken: cancellationToken));
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
