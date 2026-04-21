using Dapper;
using Npgsql;
using OtpAuth.Application.Observability;
using OtpAuth.Application.Webhooks;

namespace OtpAuth.Infrastructure.Webhooks;

internal sealed record WebhookEventDeliveryPersistenceModel
{
    public required Guid Id { get; init; }

    public required Guid SubscriptionId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string EndpointUrl { get; init; }

    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required DateTimeOffset OccurredUtc { get; init; }

    public required string ResourceType { get; init; }

    public required Guid ResourceId { get; init; }

    public required string PayloadJson { get; init; }

    public required string Status { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset NextAttemptUtc { get; init; }

    public DateTimeOffset? LastAttemptUtc { get; init; }

    public DateTimeOffset? DeliveredUtc { get; init; }

    public string? LastErrorCode { get; init; }

    public DateTimeOffset? LockedUntilUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

internal sealed record DeliveryStatusMetricsSummaryPersistenceModel
{
    public required long QueuedCount { get; init; }

    public required long DeliveredCount { get; init; }

    public required long FailedCount { get; init; }

    public required long RetryingCount { get; init; }
}

internal static class WebhookEventDeliveryDataMapper
{
    public static string ToPersistenceValue(WebhookEventDeliveryStatus status)
    {
        return status switch
        {
            WebhookEventDeliveryStatus.Queued => "queued",
            WebhookEventDeliveryStatus.Delivered => "delivered",
            WebhookEventDeliveryStatus.Failed => "failed",
            _ => throw new InvalidOperationException($"Unsupported webhook delivery status '{status}'."),
        };
    }

    public static WebhookEventDelivery ToDomainModel(WebhookEventDeliveryPersistenceModel source)
    {
        return new WebhookEventDelivery
        {
            DeliveryId = source.Id,
            SubscriptionId = source.SubscriptionId,
            TenantId = source.TenantId,
            ApplicationClientId = source.ApplicationClientId,
            EndpointUrl = new Uri(source.EndpointUrl, UriKind.Absolute),
            EventId = source.EventId,
            EventType = source.EventType,
            OccurredAtUtc = source.OccurredUtc,
            ResourceType = source.ResourceType,
            ResourceId = source.ResourceId,
            PayloadJson = source.PayloadJson,
            Status = FromPersistenceStatus(source.Status),
            AttemptCount = source.AttemptCount,
            NextAttemptUtc = source.NextAttemptUtc,
            LastAttemptUtc = source.LastAttemptUtc,
            DeliveredUtc = source.DeliveredUtc,
            LastErrorCode = source.LastErrorCode,
            LockedUntilUtc = source.LockedUntilUtc,
            CreatedUtc = source.CreatedUtc,
        };
    }

    private static WebhookEventDeliveryStatus FromPersistenceStatus(string status)
    {
        return status switch
        {
            "queued" => WebhookEventDeliveryStatus.Queued,
            "delivered" => WebhookEventDeliveryStatus.Delivered,
            "failed" => WebhookEventDeliveryStatus.Failed,
            _ => throw new InvalidOperationException($"Unsupported webhook delivery status '{status}'."),
        };
    }
}

public sealed class PostgresWebhookEventDeliveryStore : IWebhookEventDeliveryStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresWebhookEventDeliveryStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<WebhookEventDelivery>> LeaseDueAsync(
        DateTimeOffset utcNow,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var models = await connection.QueryAsync<WebhookEventDeliveryPersistenceModel>(new CommandDefinition(
            """
            with due as (
                select id
                from auth.webhook_event_deliveries
                where status = 'queued'
                  and next_attempt_utc <= @UtcNow
                  and (locked_until_utc is null or locked_until_utc <= @UtcNow)
                order by next_attempt_utc, created_utc
                limit @BatchSize
                for update skip locked
            )
            update auth.webhook_event_deliveries delivery
            set attempt_count = delivery.attempt_count + 1,
                last_attempt_utc = @UtcNow,
                locked_until_utc = @LockedUntilUtc,
                updated_utc = @UtcNow
            from due
            where delivery.id = due.id
            returning
                delivery.id as Id,
                delivery.subscription_id as SubscriptionId,
                delivery.tenant_id as TenantId,
                delivery.application_client_id as ApplicationClientId,
                delivery.endpoint_url as EndpointUrl,
                delivery.event_id as EventId,
                delivery.event_type as EventType,
                delivery.occurred_utc as OccurredUtc,
                delivery.resource_type as ResourceType,
                delivery.resource_id as ResourceId,
                delivery.payload_json::text as PayloadJson,
                delivery.status as Status,
                delivery.attempt_count as AttemptCount,
                delivery.next_attempt_utc as NextAttemptUtc,
                delivery.last_attempt_utc as LastAttemptUtc,
                delivery.delivered_utc as DeliveredUtc,
                delivery.last_error_code as LastErrorCode,
                delivery.locked_until_utc as LockedUntilUtc,
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
            .Select(WebhookEventDeliveryDataMapper.ToDomainModel)
            .ToArray();
    }

    public async Task<DeliveryStatusMetricsSummary> GetStatusMetricsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var summary = await connection.QuerySingleAsync<DeliveryStatusMetricsSummaryPersistenceModel>(new CommandDefinition(
            """
            select
                count(*) filter (where status = 'queued') as QueuedCount,
                count(*) filter (where status = 'delivered') as DeliveredCount,
                count(*) filter (where status = 'failed') as FailedCount,
                count(*) filter (where status = 'queued' and attempt_count > 0) as RetryingCount
            from auth.webhook_event_deliveries;
            """,
            cancellationToken: cancellationToken));

        return new DeliveryStatusMetricsSummary
        {
            QueuedCount = summary.QueuedCount,
            DeliveredCount = summary.DeliveredCount,
            FailedCount = summary.FailedCount,
            RetryingCount = summary.RetryingCount,
        };
    }

    public Task MarkDeliveredAsync(
        Guid deliveryId,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(
            deliveryId,
            """
            update auth.webhook_event_deliveries
            set status = 'delivered',
                delivered_utc = @DeliveredUtc,
                last_error_code = null,
                locked_until_utc = null,
                updated_utc = @DeliveredUtc
            where id = @DeliveryId;
            """,
            new
            {
                DeliveryId = deliveryId,
                DeliveredUtc = deliveredAtUtc.UtcDateTime,
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
            update auth.webhook_event_deliveries
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
            update auth.webhook_event_deliveries
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
            throw new InvalidOperationException($"Webhook event delivery '{deliveryId}' could not be updated.");
        }
    }
}
