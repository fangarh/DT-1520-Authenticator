using Dapper;
using Npgsql;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresTotpEnrollmentSeeder
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TotpSecretProtector _totpSecretProtector;

    public PostgresTotpEnrollmentSeeder(
        NpgsqlDataSource dataSource,
        TotpSecretProtector totpSecretProtector)
    {
        _dataSource = dataSource;
        _totpSecretProtector = totpSecretProtector;
    }

    public async Task UpsertAsync(
        BootstrapTotpEnrollmentSeedMaterial material,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(material);

        var protectedSecret = _totpSecretProtector.Protect(material.Secret);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.totp_enrollments (
                id,
                tenant_id,
                application_client_id,
                external_user_id,
                username,
                secret_ciphertext,
                secret_nonce,
                secret_tag,
                key_version,
                digits,
                period_seconds,
                algorithm,
                is_active,
                confirmed_utc,
                created_utc,
                updated_utc
            ) values (
                gen_random_uuid(),
                @TenantId,
                @ApplicationClientId,
                @ExternalUserId,
                @Username,
                @SecretCiphertext,
                @SecretNonce,
                @SecretTag,
                @KeyVersion,
                @Digits,
                @PeriodSeconds,
                @Algorithm,
                true,
                timezone('utc', now()),
                timezone('utc', now()),
                timezone('utc', now())
            )
            on conflict (tenant_id, application_client_id, external_user_id) do update
            set username = excluded.username,
                secret_ciphertext = excluded.secret_ciphertext,
                secret_nonce = excluded.secret_nonce,
                secret_tag = excluded.secret_tag,
                key_version = excluded.key_version,
                digits = excluded.digits,
                period_seconds = excluded.period_seconds,
                algorithm = excluded.algorithm,
                is_active = true,
                confirmed_utc = coalesce(auth.totp_enrollments.confirmed_utc, timezone('utc', now())),
                updated_utc = timezone('utc', now());
            """,
            new
            {
                material.TenantId,
                material.ApplicationClientId,
                material.ExternalUserId,
                material.Username,
                SecretCiphertext = protectedSecret.Ciphertext,
                SecretNonce = protectedSecret.Nonce,
                SecretTag = protectedSecret.Tag,
                KeyVersion = protectedSecret.KeyVersion,
                material.Digits,
                material.PeriodSeconds,
                material.Algorithm,
            },
            cancellationToken: cancellationToken));
    }
}
