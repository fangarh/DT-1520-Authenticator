using Dapper;
using Npgsql;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresBackupCodeSeeder
{
    private readonly Pbkdf2BackupCodeHasher _backupCodeHasher;
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBackupCodeSeeder(
        NpgsqlDataSource dataSource,
        Pbkdf2BackupCodeHasher backupCodeHasher)
    {
        _dataSource = dataSource;
        _backupCodeHasher = backupCodeHasher;
    }

    public async Task ReplaceActiveAsync(
        BootstrapBackupCodeSeedMaterial material,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(material);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            delete from auth.backup_codes
            where tenant_id = @TenantId
              and application_client_id = @ApplicationClientId
              and external_user_id = @ExternalUserId
              and used_utc is null;
            """,
            new
            {
                material.TenantId,
                material.ApplicationClientId,
                material.ExternalUserId,
            },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var code in material.Codes)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                insert into auth.backup_codes (
                    id,
                    tenant_id,
                    application_client_id,
                    external_user_id,
                    username,
                    code_hash,
                    created_utc,
                    used_utc
                ) values (
                    @Id,
                    @TenantId,
                    @ApplicationClientId,
                    @ExternalUserId,
                    @Username,
                    @CodeHash,
                    timezone('utc', now()),
                    null
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    material.TenantId,
                    material.ApplicationClientId,
                    material.ExternalUserId,
                    material.Username,
                    CodeHash = _backupCodeHasher.Hash(code),
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
