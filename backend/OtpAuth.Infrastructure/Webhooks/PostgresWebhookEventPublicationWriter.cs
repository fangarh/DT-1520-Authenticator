using Dapper;
using Npgsql;
using OtpAuth.Application.Webhooks;

namespace OtpAuth.Infrastructure.Webhooks;

internal static class PostgresWebhookEventPublicationWriter
{
    public static Task QueueAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WebhookEventPublication publication,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(publication);

        return connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.webhook_event_deliveries (
                id,
                subscription_id,
                tenant_id,
                application_client_id,
                endpoint_url,
                event_id,
                event_type,
                occurred_utc,
                resource_type,
                resource_id,
                payload_json,
                status,
                attempt_count,
                next_attempt_utc,
                last_attempt_utc,
                delivered_utc,
                last_error_code,
                locked_until_utc,
                created_utc,
                updated_utc
            )
            select
                gen_random_uuid(),
                subscription.id,
                @TenantId,
                @ApplicationClientId,
                subscription.endpoint_url,
                @EventId,
                @EventType,
                @OccurredUtc,
                @ResourceType,
                @ResourceId,
                cast(@PayloadJson as jsonb),
                @Status,
                0,
                @OccurredUtc,
                null,
                null,
                null,
                null,
                @OccurredUtc,
                timezone('utc', now())
            from auth.webhook_subscriptions subscription
            inner join auth.webhook_subscription_event_types event_type
                on event_type.subscription_id = subscription.id
            where subscription.tenant_id = @TenantId
              and subscription.application_client_id = @ApplicationClientId
              and subscription.status = 'active'
              and event_type.event_type = @EventType
            on conflict (subscription_id, event_id) do nothing;
            """,
            new
            {
                publication.TenantId,
                publication.ApplicationClientId,
                publication.EventId,
                publication.EventType,
                OccurredUtc = publication.OccurredAtUtc.UtcDateTime,
                publication.ResourceType,
                publication.ResourceId,
                publication.PayloadJson,
                Status = WebhookEventDeliveryStatus.Queued.ToString().ToLowerInvariant(),
            },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }
}
