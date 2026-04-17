using Dapper;
using Npgsql;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

internal sealed record PushChallengeDeliveryPersistenceModel
{
    public required Guid Id { get; init; }

    public required Guid ChallengeId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required Guid TargetDeviceId { get; init; }

    public required string Status { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset NextAttemptUtc { get; init; }

    public DateTimeOffset? LastAttemptUtc { get; init; }

    public DateTimeOffset? DeliveredUtc { get; init; }

    public string? LastErrorCode { get; init; }

    public DateTimeOffset? LockedUntilUtc { get; init; }

    public string? ProviderMessageId { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

internal static class PushChallengeDeliveryDataMapper
{
    public static PushChallengeDeliveryPersistenceModel ToPersistenceModel(PushChallengeDelivery source)
    {
        return new PushChallengeDeliveryPersistenceModel
        {
            Id = source.DeliveryId,
            ChallengeId = source.ChallengeId,
            TenantId = source.TenantId,
            ApplicationClientId = source.ApplicationClientId,
            ExternalUserId = source.ExternalUserId,
            TargetDeviceId = source.TargetDeviceId,
            Status = ToPersistenceValue(source.Status),
            AttemptCount = source.AttemptCount,
            NextAttemptUtc = source.NextAttemptUtc,
            LastAttemptUtc = source.LastAttemptUtc,
            DeliveredUtc = source.DeliveredUtc,
            LastErrorCode = source.LastErrorCode,
            LockedUntilUtc = source.LockedUntilUtc,
            ProviderMessageId = null,
            CreatedUtc = source.CreatedUtc,
        };
    }

    public static PushChallengeDelivery ToDomainModel(PushChallengeDeliveryPersistenceModel source)
    {
        return new PushChallengeDelivery
        {
            DeliveryId = source.Id,
            ChallengeId = source.ChallengeId,
            TenantId = source.TenantId,
            ApplicationClientId = source.ApplicationClientId,
            ExternalUserId = source.ExternalUserId,
            TargetDeviceId = source.TargetDeviceId,
            Status = FromPersistenceValue(source.Status),
            AttemptCount = source.AttemptCount,
            NextAttemptUtc = source.NextAttemptUtc,
            LastAttemptUtc = source.LastAttemptUtc,
            DeliveredUtc = source.DeliveredUtc,
            LastErrorCode = source.LastErrorCode,
            LockedUntilUtc = source.LockedUntilUtc,
            CreatedUtc = source.CreatedUtc,
        };
    }

    public static string ToPersistenceValue(PushChallengeDeliveryStatus status)
    {
        return status switch
        {
            PushChallengeDeliveryStatus.Queued => "queued",
            PushChallengeDeliveryStatus.Delivered => "delivered",
            PushChallengeDeliveryStatus.Failed => "failed",
            _ => throw new InvalidOperationException($"Unsupported push delivery status '{status}'."),
        };
    }

    private static PushChallengeDeliveryStatus FromPersistenceValue(string status)
    {
        return status switch
        {
            "queued" => PushChallengeDeliveryStatus.Queued,
            "delivered" => PushChallengeDeliveryStatus.Delivered,
            "failed" => PushChallengeDeliveryStatus.Failed,
            _ => throw new InvalidOperationException($"Unsupported push delivery status '{status}'."),
        };
    }
}

public sealed class PostgresPushChallengeDeliveryStore : IPushChallengeDeliveryStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPushChallengeDeliveryStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<PushChallengeDelivery>> LeaseDueAsync(
        DateTimeOffset utcNow,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var models = await connection.QueryAsync<PushChallengeDeliveryPersistenceModel>(new CommandDefinition(
            """
            with due as (
                select id
                from auth.push_challenge_deliveries
                where status = 'queued'
                  and next_attempt_utc <= @UtcNow
                  and (locked_until_utc is null or locked_until_utc <= @UtcNow)
                order by next_attempt_utc, created_utc
                limit @BatchSize
                for update skip locked
            )
            update auth.push_challenge_deliveries delivery
            set attempt_count = delivery.attempt_count + 1,
                last_attempt_utc = @UtcNow,
                locked_until_utc = @LockedUntilUtc,
                updated_utc = @UtcNow
            from due
            where delivery.id = due.id
            returning
                delivery.id as Id,
                delivery.challenge_id as ChallengeId,
                delivery.tenant_id as TenantId,
                delivery.application_client_id as ApplicationClientId,
                delivery.external_user_id as ExternalUserId,
                delivery.target_device_id as TargetDeviceId,
                delivery.status as Status,
                delivery.attempt_count as AttemptCount,
                delivery.next_attempt_utc as NextAttemptUtc,
                delivery.last_attempt_utc as LastAttemptUtc,
                delivery.delivered_utc as DeliveredUtc,
                delivery.last_error_code as LastErrorCode,
                delivery.locked_until_utc as LockedUntilUtc,
                delivery.provider_message_id as ProviderMessageId,
                delivery.created_utc as CreatedUtc;
            """,
            new
            {
                UtcNow = utcNow.UtcDateTime,
                BatchSize = batchSize,
                LockedUntilUtc = utcNow.Add(leaseDuration).UtcDateTime,
            },
            cancellationToken: cancellationToken));

        return models
            .Select(PushChallengeDeliveryDataMapper.ToDomainModel)
            .ToArray();
    }

    public Task MarkDeliveredAsync(
        Guid deliveryId,
        DateTimeOffset deliveredAtUtc,
        string? providerMessageId,
        CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(
            deliveryId,
            """
            update auth.push_challenge_deliveries
            set status = 'delivered',
                delivered_utc = @DeliveredUtc,
                last_error_code = null,
                locked_until_utc = null,
                provider_message_id = @ProviderMessageId,
                updated_utc = @DeliveredUtc
            where id = @DeliveryId;
            """,
            new
            {
                DeliveryId = deliveryId,
                DeliveredUtc = deliveredAtUtc.UtcDateTime,
                ProviderMessageId = providerMessageId,
            },
            cancellationToken);
    }

    public Task RescheduleAsync(
        Guid deliveryId,
        DateTimeOffset nextAttemptUtc,
        string errorCode,
        CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(
            deliveryId,
            """
            update auth.push_challenge_deliveries
            set status = 'queued',
                next_attempt_utc = @NextAttemptUtc,
                last_error_code = @ErrorCode,
                locked_until_utc = null,
                updated_utc = timezone('utc', now())
            where id = @DeliveryId;
            """,
            new
            {
                DeliveryId = deliveryId,
                NextAttemptUtc = nextAttemptUtc.UtcDateTime,
                ErrorCode = errorCode,
            },
            cancellationToken);
    }

    public Task MarkFailedAsync(
        Guid deliveryId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(
            deliveryId,
            """
            update auth.push_challenge_deliveries
            set status = 'failed',
                last_error_code = @ErrorCode,
                locked_until_utc = null,
                updated_utc = timezone('utc', now())
            where id = @DeliveryId;
            """,
            new
            {
                DeliveryId = deliveryId,
                ErrorCode = errorCode,
            },
            cancellationToken);
    }

    private async Task UpdateStatusAsync(
        Guid deliveryId,
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken));
        if (rowsAffected != 1)
        {
            throw new InvalidOperationException($"Push challenge delivery '{deliveryId}' could not be updated.");
        }
    }
}
