using Dapper;
using Npgsql;
using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

public sealed class PostgresAdminDeviceOnboardingStore : IAdminDeviceOnboardingStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAdminDeviceOnboardingStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<AdminDeviceOnboardingView>> ListAsync(
        AdminDeviceOnboardingListRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var records = await connection.QueryAsync<AdminDeviceOnboardingPersistenceModel>(new CommandDefinition(
            """
            select
                id as ActivationCodeId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                platform as Platform,
                expires_utc as ExpiresUtc,
                consumed_utc as ConsumedUtc,
                revoked_utc as RevokedUtc,
                created_utc as CreatedUtc
            from auth.device_activation_codes
            where tenant_id = @TenantId
              and (@ApplicationClientId is null or application_client_id = @ApplicationClientId)
              and (@ExternalUserId is null or external_user_id = @ExternalUserId)
              and (
                @Status is null
                or (@Status = 0 and revoked_utc is null and consumed_utc is null and expires_utc > @NowUtc)
                or (@Status = 1 and consumed_utc is not null)
                or (@Status = 2 and revoked_utc is null and consumed_utc is null and expires_utc <= @NowUtc)
                or (@Status = 3 and revoked_utc is not null)
              )
            order by created_utc desc, id desc
            limit @Limit;
            """,
            new
            {
                request.TenantId,
                request.ApplicationClientId,
                request.ExternalUserId,
                Status = request.Status,
                NowUtc = nowUtc.UtcDateTime,
                request.Limit,
            },
            cancellationToken: cancellationToken));

        return records
            .Select(record => AdminDeviceOnboardingDataMapper.ToDomainModel(record, nowUtc))
            .ToArray();
    }

    public async Task<AdminDeviceOnboardingView?> CreateAsync(
        AdminDeviceOnboardingCreateDraft draft,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<AdminDeviceOnboardingPersistenceModel>(new CommandDefinition(
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
                revoked_utc,
                created_utc
            ) values (
                @ActivationCodeId,
                @TenantId,
                @ApplicationClientId,
                @ExternalUserId,
                @Platform,
                @CodeHash,
                @ExpiresUtc,
                null,
                null,
                @CreatedUtc
            )
            on conflict (id) do nothing
            returning
                id as ActivationCodeId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                platform as Platform,
                expires_utc as ExpiresUtc,
                consumed_utc as ConsumedUtc,
                revoked_utc as RevokedUtc,
                created_utc as CreatedUtc;
            """,
            new
            {
                draft.ActivationCodeId,
                draft.TenantId,
                draft.ApplicationClientId,
                draft.ExternalUserId,
                draft.Platform,
                draft.CodeHash,
                ExpiresUtc = draft.ExpiresUtc.UtcDateTime,
                CreatedUtc = draft.CreatedUtc.UtcDateTime,
            },
            cancellationToken: cancellationToken));

        return record is null
            ? null
            : AdminDeviceOnboardingDataMapper.ToDomainModel(record, draft.CreatedUtc);
    }

    public async Task<AdminDeviceOnboardingRevokeStoreResult> RevokeAsync(
        Guid tenantId,
        Guid activationCodeId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var updatedRecord = await connection.QuerySingleOrDefaultAsync<AdminDeviceOnboardingPersistenceModel>(new CommandDefinition(
            """
            update auth.device_activation_codes
            set revoked_utc = @RevokedAtUtc
            where id = @ActivationCodeId
              and tenant_id = @TenantId
              and consumed_utc is null
              and revoked_utc is null
              and expires_utc > @RevokedAtUtc
            returning
                id as ActivationCodeId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                platform as Platform,
                expires_utc as ExpiresUtc,
                consumed_utc as ConsumedUtc,
                revoked_utc as RevokedUtc,
                created_utc as CreatedUtc;
            """,
            new
            {
                TenantId = tenantId,
                ActivationCodeId = activationCodeId,
                RevokedAtUtc = revokedAtUtc.UtcDateTime,
            },
            cancellationToken: cancellationToken));
        if (updatedRecord is not null)
        {
            return new AdminDeviceOnboardingRevokeStoreResult
            {
                IsFound = true,
                WasRevoked = true,
                Artifact = AdminDeviceOnboardingDataMapper.ToDomainModel(updatedRecord, revokedAtUtc),
            };
        }

        var existingRecord = await connection.QuerySingleOrDefaultAsync<AdminDeviceOnboardingPersistenceModel>(new CommandDefinition(
            """
            select
                id as ActivationCodeId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                platform as Platform,
                expires_utc as ExpiresUtc,
                consumed_utc as ConsumedUtc,
                revoked_utc as RevokedUtc,
                created_utc as CreatedUtc
            from auth.device_activation_codes
            where id = @ActivationCodeId
              and tenant_id = @TenantId
            limit 1;
            """,
            new
            {
                TenantId = tenantId,
                ActivationCodeId = activationCodeId,
            },
            cancellationToken: cancellationToken));

        return existingRecord is null
            ? new AdminDeviceOnboardingRevokeStoreResult
            {
                IsFound = false,
                WasRevoked = false,
            }
            : new AdminDeviceOnboardingRevokeStoreResult
            {
                IsFound = true,
                WasRevoked = false,
                Artifact = AdminDeviceOnboardingDataMapper.ToDomainModel(existingRecord, revokedAtUtc),
            };
    }
}
