using Dapper;
using Npgsql;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Webhooks;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class PostgresChallengeRepository : IChallengeRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresChallengeRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task AddAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        await AddAsync(challenge, pushDelivery: null, cancellationToken);
    }

    public async Task AddAsync(Challenge challenge, PushChallengeDelivery? pushDelivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var persistenceModel = ChallengeDataMapper.ToPersistenceModel(challenge);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.challenges (
                id,
                tenant_id,
                application_client_id,
                external_user_id,
                username,
                operation_type,
                operation_display_name,
                factor_type,
                status,
                expires_at,
                target_device_id,
                approved_utc,
                denied_utc,
                correlation_id,
                callback_url,
                created_utc,
                updated_utc
            ) values (
                @Id,
                @TenantId,
                @ApplicationClientId,
                @ExternalUserId,
                @Username,
                @OperationType,
                @OperationDisplayName,
                @FactorType,
                @Status,
                @ExpiresAt,
                @TargetDeviceId,
                @ApprovedUtc,
                @DeniedUtc,
                @CorrelationId,
                @CallbackUrl,
                timezone('utc', now()),
                timezone('utc', now())
            );
            """,
            persistenceModel,
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (pushDelivery is not null)
        {
            var deliveryModel = PushChallengeDeliveryDataMapper.ToPersistenceModel(pushDelivery);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                insert into auth.push_challenge_deliveries (
                    id,
                    challenge_id,
                    tenant_id,
                    application_client_id,
                    external_user_id,
                    target_device_id,
                    status,
                    attempt_count,
                    next_attempt_utc,
                    last_attempt_utc,
                    delivered_utc,
                    last_error_code,
                    locked_until_utc,
                    provider_message_id,
                    created_utc,
                    updated_utc
                ) values (
                    @Id,
                    @ChallengeId,
                    @TenantId,
                    @ApplicationClientId,
                    @ExternalUserId,
                    @TargetDeviceId,
                    @Status,
                    @AttemptCount,
                    @NextAttemptUtc,
                    @LastAttemptUtc,
                    @DeliveredUtc,
                    @LastErrorCode,
                    @LockedUntilUtc,
                    @ProviderMessageId,
                    @CreatedUtc,
                    timezone('utc', now())
                );
                """,
                deliveryModel,
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Challenge>> ListPendingPushByTargetDeviceAsync(
        Guid targetDeviceId,
        Guid tenantId,
        Guid applicationClientId,
        DateTimeOffset utcNow,
        int maxResults,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var persistenceModels = await connection.QueryAsync<ChallengePersistenceModel>(new CommandDefinition(
            """
            select
                id as Id,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                username as Username,
                operation_type as OperationType,
                operation_display_name as OperationDisplayName,
                factor_type as FactorType,
                status as Status,
                expires_at as ExpiresAt,
                target_device_id as TargetDeviceId,
                approved_utc as ApprovedUtc,
                denied_utc as DeniedUtc,
                correlation_id as CorrelationId,
                callback_url as CallbackUrl
            from auth.challenges
            where tenant_id = @TenantId
              and application_client_id = @ApplicationClientId
              and target_device_id = @TargetDeviceId
              and factor_type = @FactorType
              and status = @Status
              and expires_at > @UtcNow
            order by expires_at asc, id asc
            limit @MaxResults;
            """,
            new
            {
                TargetDeviceId = targetDeviceId,
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                FactorType = FactorType.Push,
                Status = ChallengeStatus.Pending,
                UtcNow = utcNow,
                MaxResults = maxResults,
            },
            cancellationToken: cancellationToken));

        return persistenceModels
            .Select(ChallengeDataMapper.ToDomainModel)
            .ToArray();
    }

    public async Task<Challenge?> GetByIdAsync(
        Guid challengeId,
        Guid tenantId,
        Guid applicationClientId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var persistenceModel = await connection.QuerySingleOrDefaultAsync<ChallengePersistenceModel>(new CommandDefinition(
            """
            select
                id as Id,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                external_user_id as ExternalUserId,
                username as Username,
                operation_type as OperationType,
                operation_display_name as OperationDisplayName,
                factor_type as FactorType,
                status as Status,
                expires_at as ExpiresAt,
                target_device_id as TargetDeviceId,
                approved_utc as ApprovedUtc,
                denied_utc as DeniedUtc,
                correlation_id as CorrelationId,
                callback_url as CallbackUrl
            from auth.challenges
            where id = @ChallengeId
              and tenant_id = @TenantId
              and application_client_id = @ApplicationClientId
            limit 1;
            """,
            new
            {
                ChallengeId = challengeId,
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
            },
            cancellationToken: cancellationToken));

        return persistenceModel is null
            ? null
            : ChallengeDataMapper.ToDomainModel(persistenceModel);
    }

    public async Task UpdateAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        await UpdateAsync(challenge, sideEffects: null, cancellationToken);
    }

    public async Task UpdateAsync(
        Challenge challenge,
        ChallengeUpdateSideEffects? sideEffects,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var persistenceModel = ChallengeDataMapper.ToPersistenceModel(challenge);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
            """
            update auth.challenges
            set username = @Username,
                operation_type = @OperationType,
                operation_display_name = @OperationDisplayName,
                factor_type = @FactorType,
                status = @Status,
                expires_at = @ExpiresAt,
                target_device_id = @TargetDeviceId,
                approved_utc = @ApprovedUtc,
                denied_utc = @DeniedUtc,
                correlation_id = @CorrelationId,
                callback_url = @CallbackUrl,
                updated_utc = timezone('utc', now())
            where id = @Id
              and tenant_id = @TenantId
              and application_client_id = @ApplicationClientId;
            """,
            persistenceModel,
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (rowsAffected != 1)
        {
            throw new InvalidOperationException(
                $"Challenge '{challenge.Id}' could not be updated in PostgreSQL storage.");
        }

        if (sideEffects?.CallbackDelivery is not null)
        {
            var callbackDeliveryModel = ChallengeCallbackDeliveryDataMapper.ToPersistenceModel(sideEffects.CallbackDelivery);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                insert into auth.challenge_callback_deliveries (
                    id,
                    challenge_id,
                    tenant_id,
                    application_client_id,
                    callback_url,
                    event_type,
                    occurred_utc,
                    status,
                    attempt_count,
                    next_attempt_utc,
                    last_attempt_utc,
                    delivered_utc,
                    last_error_code,
                    locked_until_utc,
                    created_utc,
                    updated_utc
                ) values (
                    @Id,
                    @ChallengeId,
                    @TenantId,
                    @ApplicationClientId,
                    @CallbackUrl,
                    @EventType,
                    @OccurredUtc,
                    @Status,
                    @AttemptCount,
                    @NextAttemptUtc,
                    @LastAttemptUtc,
                    @DeliveredUtc,
                    @LastErrorCode,
                    @LockedUntilUtc,
                    @CreatedUtc,
                    timezone('utc', now())
                )
                on conflict (challenge_id, event_type) do nothing;
                """,
                callbackDeliveryModel,
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        if (sideEffects?.WebhookEvent is not null)
        {
            await PostgresWebhookEventPublicationWriter.QueueAsync(
                connection,
                transaction,
                sideEffects.WebhookEvent,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
