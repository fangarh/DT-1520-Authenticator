using Dapper;
using Npgsql;

namespace OtpAuth.Infrastructure.Devices;

public sealed class PostgresDeviceActivationCodeSeeder
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDeviceActivationCodeSeeder(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task UpsertAsync(BootstrapDeviceActivationSeedMaterial material, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(material);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.device_activation_codes (
                id,
                tenant_id,
                application_client_id,
                external_user_id,
                platform,
                code_hash,
                expires_utc,
                consumed_utc,
                created_utc
            )
            values (
                @ActivationCodeId,
                @TenantId,
                @ApplicationClientId,
                @ExternalUserId,
                @Platform,
                @ActivationCodeHash,
                @ExpiresUtc,
                null,
                @CreatedUtc
            )
            on conflict (id) do update
            set tenant_id = excluded.tenant_id,
                application_client_id = excluded.application_client_id,
                external_user_id = excluded.external_user_id,
                platform = excluded.platform,
                code_hash = excluded.code_hash,
                expires_utc = excluded.expires_utc,
                consumed_utc = null;
            """,
            new
            {
                material.ActivationCodeId,
                material.TenantId,
                material.ApplicationClientId,
                material.ExternalUserId,
                material.Platform,
                material.ActivationCodeHash,
                material.ExpiresUtc,
                CreatedUtc = DateTimeOffset.UtcNow.UtcDateTime,
            },
            cancellationToken: cancellationToken));
    }
}
