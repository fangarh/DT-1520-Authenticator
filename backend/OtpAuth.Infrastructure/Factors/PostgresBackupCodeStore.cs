using Dapper;
using Npgsql;
using OtpAuth.Application.Factors;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresBackupCodeStore : IBackupCodeStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBackupCodeStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<BackupCodeCredential>> ListActiveAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var backupCodes = await connection.QueryAsync<BackupCodeCredential>(new CommandDefinition(
            """
            select
                id as BackupCodeId,
                code_hash as CodeHash
            from auth.backup_codes
            where tenant_id = @TenantId
              and application_client_id = @ApplicationClientId
              and external_user_id = @ExternalUserId
              and used_utc is null;
            """,
            new
            {
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                ExternalUserId = externalUserId,
            },
            cancellationToken: cancellationToken));

        return backupCodes.ToArray();
    }

    public async Task<bool> TryMarkUsedAsync(
        Guid backupCodeId,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.backup_codes
            set used_utc = @UsedAt
            where id = @BackupCodeId
              and used_utc is null;
            """,
            new
            {
                BackupCodeId = backupCodeId,
                UsedAt = usedAt,
            },
            cancellationToken: cancellationToken));

        return affectedRows == 1;
    }
}
