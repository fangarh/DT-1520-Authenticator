using Dapper;
using Npgsql;
using OtpAuth.Application.Factors;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresTotpEnrollmentStore : ITotpEnrollmentStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TotpSecretProtector _secretProtector;

    public PostgresTotpEnrollmentStore(
        NpgsqlDataSource dataSource,
        TotpSecretProtector secretProtector)
    {
        _dataSource = dataSource;
        _secretProtector = secretProtector;
    }

    public async Task<TotpEnrollmentSecret?> GetActiveAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var persistenceModel = await connection.QuerySingleOrDefaultAsync<TotpEnrollmentPersistenceModel>(new CommandDefinition(
            """
            select
                id as EnrollmentId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                username as Username,
                secret_ciphertext as SecretCiphertext,
                secret_nonce as SecretNonce,
                secret_tag as SecretTag,
                key_version as KeyVersion,
                digits as Digits,
                period_seconds as PeriodSeconds,
                algorithm as Algorithm
            from auth.totp_enrollments
            where tenant_id = @TenantId
              and application_client_id = @ApplicationClientId
              and external_user_id = @ExternalUserId
              and is_active = true
              and confirmed_utc is not null
            limit 1;
            """,
            new
            {
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                ExternalUserId = externalUserId.Trim(),
            },
            cancellationToken: cancellationToken));

        if (persistenceModel is null)
        {
            return null;
        }

        var material = TotpEnrollmentDataMapper.ToMaterial(persistenceModel);
        var secret = _secretProtector.Unprotect(new TotpProtectedSecret
        {
            Ciphertext = persistenceModel.SecretCiphertext,
            Nonce = persistenceModel.SecretNonce,
            Tag = persistenceModel.SecretTag,
            KeyVersion = persistenceModel.KeyVersion,
        });

        return new TotpEnrollmentSecret
        {
            EnrollmentId = material.EnrollmentId,
            TenantId = material.TenantId,
            ApplicationClientId = material.ApplicationClientId,
            ExternalUserId = material.ExternalUserId,
            Username = material.Username,
            Secret = secret,
            Digits = material.Digits,
            PeriodSeconds = material.PeriodSeconds,
            Algorithm = material.Algorithm,
            KeyVersion = material.KeyVersion,
        };
    }

    public async Task MarkUsedAsync(Guid enrollmentId, DateTimeOffset usedAt, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.totp_enrollments
            set last_used_utc = @UsedAt,
                updated_utc = timezone('utc', now())
            where id = @EnrollmentId;
            """,
            new
            {
                EnrollmentId = enrollmentId,
                UsedAt = usedAt,
            },
            cancellationToken: cancellationToken));
    }
}
