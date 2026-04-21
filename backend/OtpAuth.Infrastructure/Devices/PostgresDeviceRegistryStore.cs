using Dapper;
using Npgsql;
using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Webhooks;

namespace OtpAuth.Infrastructure.Devices;

public sealed class PostgresDeviceRegistryStore : IDeviceRegistryStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDeviceRegistryStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<DeviceActivationCodeArtifact?> GetActivationCodeByIdAsync(Guid activationCodeId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var activationCode = await connection.QuerySingleOrDefaultAsync<DeviceActivationCodePersistenceModel>(new CommandDefinition(
            """
            select
                id as ActivationCodeId,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                platform as Platform,
                code_hash as CodeHash,
                expires_utc as ExpiresUtc,
                consumed_utc as ConsumedUtc,
                created_utc as CreatedUtc
            from auth.device_activation_codes
            where id = @ActivationCodeId
            limit 1;
            """,
            new { ActivationCodeId = activationCodeId },
            cancellationToken: cancellationToken));

        return activationCode is null
            ? null
            : DeviceDataMapper.ToApplicationModel(activationCode);
    }

    public async Task<RegisteredDevice?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await GetDeviceAsync(connection, deviceId, tenantId: null, applicationClientId: null, cancellationToken);
    }

    public async Task<RegisteredDevice?> GetByIdAsync(Guid deviceId, Guid tenantId, Guid applicationClientId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await GetDeviceAsync(connection, deviceId, tenantId, applicationClientId, cancellationToken);
    }

    public async Task<RegisteredDevice?> GetActiveByInstallationAsync(
        Guid tenantId,
        Guid applicationClientId,
        string installationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var device = await connection.QuerySingleOrDefaultAsync<RegisteredDevicePersistenceModel>(new CommandDefinition(
            """
            select
                id as Id,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                platform as Platform,
                installation_id as InstallationId,
                device_name as DeviceName,
                status as Status,
                attestation_status as AttestationStatus,
                push_token as PushToken,
                public_key as PublicKey,
                activated_utc as ActivatedUtc,
                last_seen_utc as LastSeenUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc,
                revoked_utc as RevokedUtc,
                blocked_utc as BlockedUtc,
                created_utc as CreatedUtc
            from auth.devices
            where tenant_id = @TenantId
              and application_client_id = @ApplicationClientId
              and installation_id = @InstallationId
              and status = @ActiveStatus
            limit 1;
            """,
            new
            {
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                InstallationId = installationId,
                ActiveStatus = DeviceStatus.Active,
            },
            cancellationToken: cancellationToken));

        return device is null
            ? null
            : DeviceDataMapper.ToDomainModel(device);
    }

    public async Task<IReadOnlyCollection<RegisteredDevice>> ListActiveByExternalUserAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var devices = await connection.QueryAsync<RegisteredDevicePersistenceModel>(new CommandDefinition(
            """
            select
                id as Id,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                platform as Platform,
                installation_id as InstallationId,
                device_name as DeviceName,
                status as Status,
                attestation_status as AttestationStatus,
                push_token as PushToken,
                public_key as PublicKey,
                activated_utc as ActivatedUtc,
                last_seen_utc as LastSeenUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc,
                revoked_utc as RevokedUtc,
                blocked_utc as BlockedUtc,
                created_utc as CreatedUtc
            from auth.devices
            where tenant_id = @TenantId
              and application_client_id = @ApplicationClientId
              and external_user_id = @ExternalUserId
              and status = @ActiveStatus;
            """,
            new
            {
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                ExternalUserId = externalUserId,
                ActiveStatus = DeviceStatus.Active,
            },
            cancellationToken: cancellationToken));

        return devices
            .Select(DeviceDataMapper.ToDomainModel)
            .ToArray();
    }

    public async Task<DeviceRefreshTokenRecord?> GetRefreshTokenByIdAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var token = await connection.QuerySingleOrDefaultAsync<DeviceRefreshTokenPersistenceModel>(new CommandDefinition(
            """
            select
                id as TokenId,
                device_id as DeviceId,
                token_family_id as TokenFamilyId,
                token_hash as TokenHash,
                issued_utc as IssuedUtc,
                expires_utc as ExpiresUtc,
                consumed_utc as ConsumedUtc,
                revoked_utc as RevokedUtc,
                replaced_by_token_id as ReplacedByTokenId,
                created_utc as CreatedUtc
            from auth.device_refresh_tokens
            where id = @TokenId
            limit 1;
            """,
            new { TokenId = tokenId },
            cancellationToken: cancellationToken));

        return token is null
            ? null
            : DeviceDataMapper.ToApplicationModel(token);
    }

    public async Task<bool> ActivateAsync(
        RegisteredDevice device,
        DeviceRefreshTokenRecord refreshToken,
        Guid activationCodeId,
        DateTimeOffset activatedAtUtc,
        DeviceLifecycleSideEffects? sideEffects,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var activationCodeConsumed = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.device_activation_codes
            set consumed_utc = @ActivatedAtUtc
            where id = @ActivationCodeId
              and consumed_utc is null
              and expires_utc > @ActivatedAtUtc;
            """,
            new
            {
                ActivationCodeId = activationCodeId,
                ActivatedAtUtc = activatedAtUtc.UtcDateTime,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
        if (activationCodeConsumed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var deviceModel = DeviceDataMapper.ToPersistenceModel(device);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.devices (
                id,
                tenant_id,
                application_client_id,
                external_user_id,
                platform,
                installation_id,
                device_name,
                status,
                attestation_status,
                push_token,
                public_key,
                activated_utc,
                last_seen_utc,
                last_auth_state_changed_utc,
                revoked_utc,
                blocked_utc,
                created_utc
            )
            values (
                @Id,
                @TenantId,
                @ApplicationClientId,
                @ExternalUserId,
                @Platform,
                @InstallationId,
                @DeviceName,
                @Status,
                @AttestationStatus,
                @PushToken,
                @PublicKey,
                @ActivatedUtc,
                @LastSeenUtc,
                @LastAuthStateChangedUtc,
                @RevokedUtc,
                @BlockedUtc,
                @CreatedUtc
            );
            """,
            deviceModel,
            transaction: transaction,
            cancellationToken: cancellationToken));

        var tokenModel = DeviceDataMapper.ToPersistenceModel(refreshToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.device_refresh_tokens (
                id,
                device_id,
                token_family_id,
                token_hash,
                issued_utc,
                expires_utc,
                consumed_utc,
                revoked_utc,
                replaced_by_token_id,
                created_utc
            )
            values (
                @TokenId,
                @DeviceId,
                @TokenFamilyId,
                @TokenHash,
                @IssuedUtc,
                @ExpiresUtc,
                @ConsumedUtc,
                @RevokedUtc,
                @ReplacedByTokenId,
                @CreatedUtc
            );
            """,
            tokenModel,
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (sideEffects?.WebhookEvent is not null)
        {
            await PostgresWebhookEventPublicationWriter.QueueAsync(
                connection,
                transaction,
                sideEffects.WebhookEvent,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RotateRefreshTokenAsync(
        DeviceRefreshRotation rotation,
        Guid deviceId,
        DateTimeOffset lastSeenUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var currentTokenUpdated = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.device_refresh_tokens
            set consumed_utc = @RotatedAtUtc,
                replaced_by_token_id = @ReplacedByTokenId
            where id = @CurrentTokenId
              and consumed_utc is null
              and revoked_utc is null
              and expires_utc > @RotatedAtUtc;
            """,
            new
            {
                rotation.CurrentTokenId,
                RotatedAtUtc = rotation.RotatedAtUtc.UtcDateTime,
                ReplacedByTokenId = rotation.ReplacedByTokenId,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
        if (currentTokenUpdated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var newTokenModel = DeviceDataMapper.ToPersistenceModel(rotation.NewToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.device_refresh_tokens (
                id,
                device_id,
                token_family_id,
                token_hash,
                issued_utc,
                expires_utc,
                consumed_utc,
                revoked_utc,
                replaced_by_token_id,
                created_utc
            )
            values (
                @TokenId,
                @DeviceId,
                @TokenFamilyId,
                @TokenHash,
                @IssuedUtc,
                @ExpiresUtc,
                @ConsumedUtc,
                @RevokedUtc,
                @ReplacedByTokenId,
                @CreatedUtc
            );
            """,
            newTokenModel,
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.devices
            set last_seen_utc = @LastSeenUtc
            where id = @DeviceId;
            """,
            new
            {
                DeviceId = deviceId,
                LastSeenUtc = lastSeenUtc.UtcDateTime,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RevokeDeviceAsync(
        RegisteredDevice device,
        DateTimeOffset revokedAtUtc,
        DeviceLifecycleSideEffects? sideEffects,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var updatedDevices = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.devices
            set status = @Status,
                revoked_utc = @RevokedUtc,
                last_auth_state_changed_utc = @LastAuthStateChangedUtc
            where id = @Id;
            """,
            new
            {
                device.Id,
                Status = device.Status,
                RevokedUtc = device.RevokedUtc?.UtcDateTime,
                LastAuthStateChangedUtc = device.LastAuthStateChangedUtc.UtcDateTime,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
        if (updatedDevices != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.device_refresh_tokens
            set revoked_utc = @RevokedAtUtc
            where device_id = @DeviceId
              and revoked_utc is null;
            """,
            new
            {
                DeviceId = device.Id,
                RevokedAtUtc = revokedAtUtc.UtcDateTime,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (sideEffects?.WebhookEvent is not null)
        {
            await PostgresWebhookEventPublicationWriter.QueueAsync(
                connection,
                transaction,
                sideEffects.WebhookEvent,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> BlockDeviceAsync(
        RegisteredDevice device,
        DateTimeOffset blockedAtUtc,
        DeviceLifecycleSideEffects? sideEffects,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var updatedDevices = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.devices
            set status = @Status,
                blocked_utc = @BlockedUtc,
                last_auth_state_changed_utc = @LastAuthStateChangedUtc
            where id = @Id;
            """,
            new
            {
                device.Id,
                Status = device.Status,
                BlockedUtc = device.BlockedUtc?.UtcDateTime,
                LastAuthStateChangedUtc = device.LastAuthStateChangedUtc.UtcDateTime,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
        if (updatedDevices != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.device_refresh_tokens
            set revoked_utc = @BlockedAtUtc
            where device_id = @DeviceId
              and revoked_utc is null;
            """,
            new
            {
                DeviceId = device.Id,
                BlockedAtUtc = blockedAtUtc.UtcDateTime,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (sideEffects?.WebhookEvent is not null)
        {
            await PostgresWebhookEventPublicationWriter.QueueAsync(
                connection,
                transaction,
                sideEffects.WebhookEvent,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<RegisteredDevice?> GetDeviceAsync(
        NpgsqlConnection connection,
        Guid deviceId,
        Guid? tenantId,
        Guid? applicationClientId,
        CancellationToken cancellationToken)
    {
        var device = await connection.QuerySingleOrDefaultAsync<RegisteredDevicePersistenceModel>(new CommandDefinition(
            """
            select
                id as Id,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                platform as Platform,
                installation_id as InstallationId,
                device_name as DeviceName,
                status as Status,
                attestation_status as AttestationStatus,
                push_token as PushToken,
                public_key as PublicKey,
                activated_utc as ActivatedUtc,
                last_seen_utc as LastSeenUtc,
                last_auth_state_changed_utc as LastAuthStateChangedUtc,
                revoked_utc as RevokedUtc,
                blocked_utc as BlockedUtc,
                created_utc as CreatedUtc
            from auth.devices
            where id = @DeviceId
              and (@TenantId is null or tenant_id = @TenantId)
              and (@ApplicationClientId is null or application_client_id = @ApplicationClientId)
            limit 1;
            """,
            new
            {
                DeviceId = deviceId,
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
            },
            cancellationToken: cancellationToken));

        return device is null
            ? null
            : DeviceDataMapper.ToDomainModel(device);
    }
}
