using Dapper;
using Npgsql;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class PostgresIntegrationClientLifecycleStore : IIntegrationClientLifecycleStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresIntegrationClientLifecycleStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<ManagedIntegrationClient?> GetManagedClientByIdAsync(string clientId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ManagedIntegrationClient>(new CommandDefinition(
            """
            select
                client_id as ClientId,
                is_active as IsActive,
                last_secret_rotated_utc as LastSecretRotatedUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc
            from auth.integration_clients
            where client_id = @ClientId
            limit 1;
            """,
            new { ClientId = clientId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> RotateSecretAsync(
        string clientId,
        string clientSecretHash,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.integration_clients
            set client_secret_hash = @ClientSecretHash,
                updated_utc = @ChangedAtUtc,
                last_secret_rotated_utc = @ChangedAtUtc,
                last_auth_state_changed_utc = @ChangedAtUtc
            where client_id = @ClientId;
            """,
            new
            {
                ClientId = clientId,
                ClientSecretHash = clientSecretHash,
                ChangedAtUtc = changedAtUtc.UtcDateTime,
            },
            cancellationToken: cancellationToken));

        return rowsAffected == 1;
    }

    public async Task<bool> SetIsActiveAsync(
        string clientId,
        bool isActive,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.integration_clients
            set is_active = @IsActive,
                updated_utc = @ChangedAtUtc,
                last_auth_state_changed_utc = @ChangedAtUtc
            where client_id = @ClientId;
            """,
            new
            {
                ClientId = clientId,
                IsActive = isActive,
                ChangedAtUtc = changedAtUtc.UtcDateTime,
            },
            cancellationToken: cancellationToken));

        return rowsAffected == 1;
    }
}
