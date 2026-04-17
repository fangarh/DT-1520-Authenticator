using Dapper;
using Npgsql;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresTotpEnrollmentMaintenanceStore : ITotpEnrollmentMaintenanceStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTotpEnrollmentMaintenanceStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<TotpEnrollmentProtectedRecord>> GetRecordsRequiringReEncryptionAsync(
        int currentKeyVersion,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var records = await connection.QueryAsync<TotpEnrollmentProtectedRecord>(new CommandDefinition(
            """
            select
                id as EnrollmentId,
                key_version as KeyVersion,
                secret_ciphertext as Ciphertext,
                secret_nonce as Nonce,
                secret_tag as Tag
            from auth.totp_enrollments
            where key_version <> @CurrentKeyVersion
            order by updated_utc, id
            limit @BatchSize;
            """,
            new
            {
                CurrentKeyVersion = currentKeyVersion,
                BatchSize = batchSize,
            },
            cancellationToken: cancellationToken));

        return records.ToArray();
    }

    public async Task<IReadOnlyCollection<TotpEnrollmentKeyVersionUsage>> GetKeyVersionUsageAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var records = await connection.QueryAsync<TotpEnrollmentKeyVersionUsage>(new CommandDefinition(
            """
            select
                key_version as KeyVersion,
                count(*)::integer as EnrollmentCount
            from auth.totp_enrollments
            group by key_version
            order by key_version;
            """,
            cancellationToken: cancellationToken));

        return records.ToArray();
    }

    public async Task<bool> UpdateProtectedSecretAsync(
        Guid enrollmentId,
        int expectedKeyVersion,
        TotpProtectedSecret protectedSecret,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.totp_enrollments
            set secret_ciphertext = @Ciphertext,
                secret_nonce = @Nonce,
                secret_tag = @Tag,
                key_version = @KeyVersion,
                updated_utc = timezone('utc', now())
            where id = @EnrollmentId
              and key_version = @ExpectedKeyVersion;
            """,
            new
            {
                EnrollmentId = enrollmentId,
                ExpectedKeyVersion = expectedKeyVersion,
                Ciphertext = protectedSecret.Ciphertext,
                Nonce = protectedSecret.Nonce,
                Tag = protectedSecret.Tag,
                KeyVersion = protectedSecret.KeyVersion,
            },
            cancellationToken: cancellationToken));

        return rowsAffected == 1;
    }
}
