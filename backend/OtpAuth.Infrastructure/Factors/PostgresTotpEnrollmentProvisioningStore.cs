using Dapper;
using Npgsql;
using OtpAuth.Application.Enrollments;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresTotpEnrollmentProvisioningStore : ITotpEnrollmentProvisioningStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TotpSecretProtector _secretProtector;

    public PostgresTotpEnrollmentProvisioningStore(
        NpgsqlDataSource dataSource,
        TotpSecretProtector secretProtector)
    {
        _dataSource = dataSource;
        _secretProtector = secretProtector;
    }

    public async Task<TotpEnrollmentProvisioningRecord?> GetByIdAsync(
        Guid enrollmentId,
        Guid tenantId,
        Guid applicationClientId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var model = await connection.QuerySingleOrDefaultAsync<TotpEnrollmentProvisioningPersistenceModel>(new CommandDefinition(
            """
            select
                id as EnrollmentId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                username as Label,
                secret_ciphertext as SecretCiphertext,
                secret_nonce as SecretNonce,
                secret_tag as SecretTag,
                key_version as KeyVersion,
                digits as Digits,
                period_seconds as PeriodSeconds,
                algorithm as Algorithm,
                replacement_secret_ciphertext as ReplacementSecretCiphertext,
                replacement_secret_nonce as ReplacementSecretNonce,
                replacement_secret_tag as ReplacementSecretTag,
                replacement_key_version as ReplacementKeyVersion,
                replacement_digits as ReplacementDigits,
                replacement_period_seconds as ReplacementPeriodSeconds,
                replacement_algorithm as ReplacementAlgorithm,
                replacement_started_utc as ReplacementStartedUtc,
                replacement_failed_confirm_attempts as ReplacementFailedConfirmationAttempts,
                is_active as IsActive,
                confirmed_utc as ConfirmedUtc,
                revoked_utc as RevokedUtc,
                failed_confirm_attempts as FailedConfirmationAttempts
            from auth.totp_enrollments
            where id = @EnrollmentId
              and tenant_id = @TenantId
              and application_client_id = @ApplicationClientId
            limit 1;
            """,
            new
            {
                EnrollmentId = enrollmentId,
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
            },
            cancellationToken: cancellationToken));

        return model is null ? null : Map(model);
    }

    public async Task<TotpEnrollmentProvisioningRecord?> GetByIdForAdminAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var model = await connection.QuerySingleOrDefaultAsync<TotpEnrollmentProvisioningPersistenceModel>(new CommandDefinition(
            """
            select
                id as EnrollmentId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                username as Label,
                secret_ciphertext as SecretCiphertext,
                secret_nonce as SecretNonce,
                secret_tag as SecretTag,
                key_version as KeyVersion,
                digits as Digits,
                period_seconds as PeriodSeconds,
                algorithm as Algorithm,
                replacement_secret_ciphertext as ReplacementSecretCiphertext,
                replacement_secret_nonce as ReplacementSecretNonce,
                replacement_secret_tag as ReplacementSecretTag,
                replacement_key_version as ReplacementKeyVersion,
                replacement_digits as ReplacementDigits,
                replacement_period_seconds as ReplacementPeriodSeconds,
                replacement_algorithm as ReplacementAlgorithm,
                replacement_started_utc as ReplacementStartedUtc,
                replacement_failed_confirm_attempts as ReplacementFailedConfirmationAttempts,
                is_active as IsActive,
                confirmed_utc as ConfirmedUtc,
                revoked_utc as RevokedUtc,
                failed_confirm_attempts as FailedConfirmationAttempts
            from auth.totp_enrollments
            where id = @EnrollmentId
            limit 1;
            """,
            new { EnrollmentId = enrollmentId },
            cancellationToken: cancellationToken));

        return model is null ? null : Map(model);
    }

    public async Task<TotpEnrollmentProvisioningRecord?> GetByExternalUserIdAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var model = await connection.QuerySingleOrDefaultAsync<TotpEnrollmentProvisioningPersistenceModel>(new CommandDefinition(
            """
            select
                id as EnrollmentId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                username as Label,
                secret_ciphertext as SecretCiphertext,
                secret_nonce as SecretNonce,
                secret_tag as SecretTag,
                key_version as KeyVersion,
                digits as Digits,
                period_seconds as PeriodSeconds,
                algorithm as Algorithm,
                replacement_secret_ciphertext as ReplacementSecretCiphertext,
                replacement_secret_nonce as ReplacementSecretNonce,
                replacement_secret_tag as ReplacementSecretTag,
                replacement_key_version as ReplacementKeyVersion,
                replacement_digits as ReplacementDigits,
                replacement_period_seconds as ReplacementPeriodSeconds,
                replacement_algorithm as ReplacementAlgorithm,
                replacement_started_utc as ReplacementStartedUtc,
                replacement_failed_confirm_attempts as ReplacementFailedConfirmationAttempts,
                is_active as IsActive,
                confirmed_utc as ConfirmedUtc,
                revoked_utc as RevokedUtc,
                failed_confirm_attempts as FailedConfirmationAttempts
            from auth.totp_enrollments
            where tenant_id = @TenantId
              and application_client_id = @ApplicationClientId
              and external_user_id = @ExternalUserId
            limit 1;
            """,
            new
            {
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                ExternalUserId = externalUserId.Trim(),
            },
            cancellationToken: cancellationToken));

        return model is null ? null : Map(model);
    }

    public async Task<TotpEnrollmentProvisioningRecord?> GetCurrentByExternalUserIdAsync(
        Guid tenantId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var model = await connection.QuerySingleOrDefaultAsync<TotpEnrollmentProvisioningPersistenceModel>(new CommandDefinition(
            """
            select
                id as EnrollmentId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                username as Label,
                secret_ciphertext as SecretCiphertext,
                secret_nonce as SecretNonce,
                secret_tag as SecretTag,
                key_version as KeyVersion,
                digits as Digits,
                period_seconds as PeriodSeconds,
                algorithm as Algorithm,
                replacement_secret_ciphertext as ReplacementSecretCiphertext,
                replacement_secret_nonce as ReplacementSecretNonce,
                replacement_secret_tag as ReplacementSecretTag,
                replacement_key_version as ReplacementKeyVersion,
                replacement_digits as ReplacementDigits,
                replacement_period_seconds as ReplacementPeriodSeconds,
                replacement_algorithm as ReplacementAlgorithm,
                replacement_started_utc as ReplacementStartedUtc,
                replacement_failed_confirm_attempts as ReplacementFailedConfirmationAttempts,
                is_active as IsActive,
                confirmed_utc as ConfirmedUtc,
                revoked_utc as RevokedUtc,
                failed_confirm_attempts as FailedConfirmationAttempts
            from auth.totp_enrollments
            where tenant_id = @TenantId
              and external_user_id = @ExternalUserId
            order by
                is_active desc,
                confirmed_utc desc nulls last,
                revoked_utc desc nulls last,
                updated_utc desc,
                id desc
            limit 1;
            """,
            new
            {
                TenantId = tenantId,
                ExternalUserId = externalUserId.Trim(),
            },
            cancellationToken: cancellationToken));

        return model is null ? null : Map(model);
    }

    public async Task<TotpEnrollmentProvisioningRecord> UpsertPendingAsync(
        TotpEnrollmentProvisioningDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var protectedSecret = _secretProtector.Protect(draft.Secret);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var enrollmentId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
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
                revoked_utc,
                last_used_utc,
                failed_confirm_attempts,
                created_utc,
                updated_utc
            )
            values (
                gen_random_uuid(),
                @TenantId,
                @ApplicationClientId,
                @ExternalUserId,
                @Label,
                @SecretCiphertext,
                @SecretNonce,
                @SecretTag,
                @KeyVersion,
                @Digits,
                @PeriodSeconds,
                @Algorithm,
                true,
                null,
                null,
                null,
                0,
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
                confirmed_utc = null,
                revoked_utc = null,
                last_used_utc = null,
                failed_confirm_attempts = 0,
                updated_utc = timezone('utc', now())
            returning id;
            """,
            new
            {
                draft.TenantId,
                draft.ApplicationClientId,
                ExternalUserId = draft.ExternalUserId.Trim(),
                draft.Label,
                SecretCiphertext = protectedSecret.Ciphertext,
                SecretNonce = protectedSecret.Nonce,
                SecretTag = protectedSecret.Tag,
                KeyVersion = protectedSecret.KeyVersion,
                draft.Digits,
                draft.PeriodSeconds,
                draft.Algorithm,
            },
            cancellationToken: cancellationToken));

        return new TotpEnrollmentProvisioningRecord
        {
            EnrollmentId = enrollmentId,
            TenantId = draft.TenantId,
            ApplicationClientId = draft.ApplicationClientId,
            ExternalUserId = draft.ExternalUserId.Trim(),
            Label = draft.Label,
            Secret = draft.Secret,
            Digits = draft.Digits,
            PeriodSeconds = draft.PeriodSeconds,
            Algorithm = draft.Algorithm,
            IsActive = true,
            ConfirmedUtc = null,
            FailedConfirmationAttempts = 0,
            PendingReplacement = null,
        };
    }

    public async Task<TotpEnrollmentProvisioningRecord> UpsertPendingReplacementAsync(
        TotpEnrollmentReplacementDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var protectedSecret = _secretProtector.Protect(draft.Secret);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.totp_enrollments
            set replacement_secret_ciphertext = @SecretCiphertext,
                replacement_secret_nonce = @SecretNonce,
                replacement_secret_tag = @SecretTag,
                replacement_key_version = @KeyVersion,
                replacement_digits = @Digits,
                replacement_period_seconds = @PeriodSeconds,
                replacement_algorithm = @Algorithm,
                replacement_started_utc = timezone('utc', now()),
                replacement_failed_confirm_attempts = 0,
                updated_utc = timezone('utc', now())
            where id = @EnrollmentId;
            """,
            new
            {
                draft.EnrollmentId,
                SecretCiphertext = protectedSecret.Ciphertext,
                SecretNonce = protectedSecret.Nonce,
                SecretTag = protectedSecret.Tag,
                KeyVersion = protectedSecret.KeyVersion,
                draft.Digits,
                draft.PeriodSeconds,
                draft.Algorithm,
            },
            cancellationToken: cancellationToken));

        var enrollment = await connection.QuerySingleAsync<TotpEnrollmentProvisioningPersistenceModel>(new CommandDefinition(
            """
            select
                id as EnrollmentId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                username as Label,
                secret_ciphertext as SecretCiphertext,
                secret_nonce as SecretNonce,
                secret_tag as SecretTag,
                key_version as KeyVersion,
                digits as Digits,
                period_seconds as PeriodSeconds,
                algorithm as Algorithm,
                replacement_secret_ciphertext as ReplacementSecretCiphertext,
                replacement_secret_nonce as ReplacementSecretNonce,
                replacement_secret_tag as ReplacementSecretTag,
                replacement_key_version as ReplacementKeyVersion,
                replacement_digits as ReplacementDigits,
                replacement_period_seconds as ReplacementPeriodSeconds,
                replacement_algorithm as ReplacementAlgorithm,
                replacement_started_utc as ReplacementStartedUtc,
                replacement_failed_confirm_attempts as ReplacementFailedConfirmationAttempts,
                is_active as IsActive,
                confirmed_utc as ConfirmedUtc,
                revoked_utc as RevokedUtc,
                failed_confirm_attempts as FailedConfirmationAttempts
            from auth.totp_enrollments
            where id = @EnrollmentId
            limit 1;
            """,
            new { draft.EnrollmentId },
            cancellationToken: cancellationToken));

        return Map(enrollment);
    }

    public async Task<bool> ConfirmAsync(
        Guid enrollmentId,
        DateTimeOffset confirmedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.totp_enrollments
            set confirmed_utc = @ConfirmedAt,
                revoked_utc = null,
                updated_utc = timezone('utc', now())
            where id = @EnrollmentId
              and confirmed_utc is null
              and is_active = true;
            """,
            new
            {
                EnrollmentId = enrollmentId,
                ConfirmedAt = confirmedAt,
            },
            cancellationToken: cancellationToken));

        return rowsAffected == 1;
    }

    public async Task<bool> RevokeAsync(
        Guid enrollmentId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.totp_enrollments
            set is_active = false,
                revoked_utc = @RevokedAt,
                replacement_secret_ciphertext = null,
                replacement_secret_nonce = null,
                replacement_secret_tag = null,
                replacement_key_version = null,
                replacement_digits = null,
                replacement_period_seconds = null,
                replacement_algorithm = null,
                replacement_started_utc = null,
                replacement_failed_confirm_attempts = 0,
                updated_utc = timezone('utc', now())
            where id = @EnrollmentId
              and is_active = true;
            """,
            new
            {
                EnrollmentId = enrollmentId,
                RevokedAt = revokedAt,
            },
            cancellationToken: cancellationToken));

        return rowsAffected == 1;
    }

    public async Task<bool> ConfirmReplacementAsync(
        Guid enrollmentId,
        DateTimeOffset confirmedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.totp_enrollments
            set secret_ciphertext = replacement_secret_ciphertext,
                secret_nonce = replacement_secret_nonce,
                secret_tag = replacement_secret_tag,
                key_version = replacement_key_version,
                digits = replacement_digits,
                period_seconds = replacement_period_seconds,
                algorithm = replacement_algorithm,
                confirmed_utc = @ConfirmedAt,
                revoked_utc = null,
                replacement_secret_ciphertext = null,
                replacement_secret_nonce = null,
                replacement_secret_tag = null,
                replacement_key_version = null,
                replacement_digits = null,
                replacement_period_seconds = null,
                replacement_algorithm = null,
                replacement_started_utc = null,
                replacement_failed_confirm_attempts = 0,
                updated_utc = timezone('utc', now())
            where id = @EnrollmentId
              and is_active = true
              and replacement_started_utc is not null
              and replacement_secret_ciphertext is not null
              and replacement_secret_nonce is not null
              and replacement_secret_tag is not null
              and replacement_key_version is not null
              and replacement_digits is not null
              and replacement_period_seconds is not null
              and replacement_algorithm is not null;
            """,
            new
            {
                EnrollmentId = enrollmentId,
                ConfirmedAt = confirmedAt,
            },
            cancellationToken: cancellationToken));

        return rowsAffected == 1;
    }

    public async Task IncrementFailedConfirmationAttemptsAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.totp_enrollments
            set failed_confirm_attempts = failed_confirm_attempts + 1,
                updated_utc = timezone('utc', now())
            where id = @EnrollmentId;
            """,
            new { EnrollmentId = enrollmentId },
            cancellationToken: cancellationToken));
    }

    public async Task IncrementFailedReplacementConfirmationAttemptsAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.totp_enrollments
            set replacement_failed_confirm_attempts = replacement_failed_confirm_attempts + 1,
                updated_utc = timezone('utc', now())
            where id = @EnrollmentId;
            """,
            new { EnrollmentId = enrollmentId },
            cancellationToken: cancellationToken));
    }

    private TotpEnrollmentProvisioningRecord Map(TotpEnrollmentProvisioningPersistenceModel model)
    {
        var secret = _secretProtector.Unprotect(new TotpProtectedSecret
        {
            Ciphertext = model.SecretCiphertext,
            Nonce = model.SecretNonce,
            Tag = model.SecretTag,
            KeyVersion = model.KeyVersion,
        });

        return new TotpEnrollmentProvisioningRecord
        {
            EnrollmentId = model.EnrollmentId,
            TenantId = model.TenantId,
            ApplicationClientId = model.ApplicationClientId,
            ExternalUserId = model.ExternalUserId,
            Label = model.Label,
            Secret = secret,
            Digits = model.Digits,
            PeriodSeconds = model.PeriodSeconds,
            Algorithm = model.Algorithm,
            IsActive = model.IsActive,
            ConfirmedUtc = model.ConfirmedUtc,
            FailedConfirmationAttempts = model.FailedConfirmationAttempts,
            PendingReplacement = MapPendingReplacement(model),
            RevokedUtc = model.RevokedUtc,
        };
    }

    private TotpPendingReplacementRecord? MapPendingReplacement(TotpEnrollmentProvisioningPersistenceModel model)
    {
        if (model.ReplacementStartedUtc is null ||
            model.ReplacementSecretCiphertext is null ||
            model.ReplacementSecretNonce is null ||
            model.ReplacementSecretTag is null ||
            model.ReplacementKeyVersion is null ||
            model.ReplacementDigits is null ||
            model.ReplacementPeriodSeconds is null ||
            string.IsNullOrWhiteSpace(model.ReplacementAlgorithm))
        {
            return null;
        }

        var secret = _secretProtector.Unprotect(new TotpProtectedSecret
        {
            Ciphertext = model.ReplacementSecretCiphertext,
            Nonce = model.ReplacementSecretNonce,
            Tag = model.ReplacementSecretTag,
            KeyVersion = model.ReplacementKeyVersion.Value,
        });

        return new TotpPendingReplacementRecord
        {
            Secret = secret,
            Digits = model.ReplacementDigits.Value,
            PeriodSeconds = model.ReplacementPeriodSeconds.Value,
            Algorithm = model.ReplacementAlgorithm.Trim(),
            StartedUtc = model.ReplacementStartedUtc.Value,
            FailedConfirmationAttempts = model.ReplacementFailedConfirmationAttempts,
        };
    }

    private sealed record TotpEnrollmentProvisioningPersistenceModel
    {
        public required Guid EnrollmentId { get; init; }

        public required Guid TenantId { get; init; }

        public required Guid ApplicationClientId { get; init; }

        public required string ExternalUserId { get; init; }

        public string? Label { get; init; }

        public required byte[] SecretCiphertext { get; init; }

        public required byte[] SecretNonce { get; init; }

        public required byte[] SecretTag { get; init; }

        public required int KeyVersion { get; init; }

        public required int Digits { get; init; }

        public required int PeriodSeconds { get; init; }

        public required string Algorithm { get; init; }

        public byte[]? ReplacementSecretCiphertext { get; init; }

        public byte[]? ReplacementSecretNonce { get; init; }

        public byte[]? ReplacementSecretTag { get; init; }

        public int? ReplacementKeyVersion { get; init; }

        public int? ReplacementDigits { get; init; }

        public int? ReplacementPeriodSeconds { get; init; }

        public string? ReplacementAlgorithm { get; init; }

        public DateTimeOffset? ReplacementStartedUtc { get; init; }

        public int ReplacementFailedConfirmationAttempts { get; init; }

        public required bool IsActive { get; init; }

        public DateTimeOffset? ConfirmedUtc { get; init; }

        public DateTimeOffset? RevokedUtc { get; init; }

        public int FailedConfirmationAttempts { get; init; }
    }
}
