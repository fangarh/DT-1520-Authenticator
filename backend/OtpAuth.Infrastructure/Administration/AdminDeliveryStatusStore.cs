using Dapper;
using Npgsql;
using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

public sealed class PostgresAdminDeliveryStatusStore : IAdminDeliveryStatusStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAdminDeliveryStatusStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<AdminDeliveryStatusView>> ListRecentAsync(
        AdminDeliveryStatusListRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var models = await connection.QueryAsync<AdminDeliveryStatusPersistenceModel>(new CommandDefinition(
            """
            select
                deliveries.delivery_id as DeliveryId,
                deliveries.tenant_id as TenantId,
                deliveries.application_client_id as ApplicationClientId,
                deliveries.channel as Channel,
                deliveries.status as Status,
                deliveries.event_type as EventType,
                deliveries.destination_url as DestinationUrl,
                deliveries.subject_type as SubjectType,
                deliveries.subject_id as SubjectId,
                deliveries.publication_id as PublicationId,
                deliveries.attempt_count as AttemptCount,
                deliveries.occurred_utc as OccurredAtUtc,
                deliveries.created_utc as CreatedAtUtc,
                deliveries.next_attempt_utc as NextAttemptAtUtc,
                deliveries.last_attempt_utc as LastAttemptAtUtc,
                deliveries.delivered_utc as DeliveredAtUtc,
                deliveries.last_error_code as LastErrorCode
            from (
                select
                    callback.id as delivery_id,
                    callback.tenant_id,
                    callback.application_client_id,
                    'challenge_callback' as channel,
                    callback.status,
                    callback.event_type,
                    callback.callback_url as destination_url,
                    'challenge' as subject_type,
                    callback.challenge_id as subject_id,
                    null::uuid as publication_id,
                    callback.attempt_count,
                    callback.occurred_utc,
                    callback.created_utc,
                    callback.next_attempt_utc,
                    callback.last_attempt_utc,
                    callback.delivered_utc,
                    callback.last_error_code
                from auth.challenge_callback_deliveries callback

                union all

                select
                    webhook.id as delivery_id,
                    webhook.tenant_id,
                    webhook.application_client_id,
                    'webhook_event' as channel,
                    webhook.status,
                    webhook.event_type,
                    webhook.endpoint_url as destination_url,
                    webhook.resource_type as subject_type,
                    webhook.resource_id as subject_id,
                    webhook.event_id as publication_id,
                    webhook.attempt_count,
                    webhook.occurred_utc,
                    webhook.created_utc,
                    webhook.next_attempt_utc,
                    webhook.last_attempt_utc,
                    webhook.delivered_utc,
                    webhook.last_error_code
                from auth.webhook_event_deliveries webhook
            ) deliveries
            where deliveries.tenant_id = @TenantId
              and (@ApplicationClientId is null or deliveries.application_client_id = @ApplicationClientId)
              and (@Channel is null or deliveries.channel = @Channel)
              and (@Status is null or deliveries.status = @Status)
            order by deliveries.created_utc desc, deliveries.delivery_id desc
            limit @Limit;
            """,
            new
            {
                request.TenantId,
                request.ApplicationClientId,
                Channel = AdminDeliveryStatusDataMapper.ToPersistenceValue(request.Channel),
                Status = AdminDeliveryStatusDataMapper.ToPersistenceValue(request.Status),
                request.Limit,
            },
            cancellationToken: cancellationToken));

        return models
            .Select(AdminDeliveryStatusDataMapper.ToDomainModel)
            .ToArray();
    }
}
