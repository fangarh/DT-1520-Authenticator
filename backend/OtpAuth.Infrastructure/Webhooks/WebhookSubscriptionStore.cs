using Dapper;
using Npgsql;
using OtpAuth.Application.Webhooks;

namespace OtpAuth.Infrastructure.Webhooks;

internal sealed record WebhookSubscriptionPersistenceModel
{
    public required Guid Id { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string EndpointUrl { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }
}

internal sealed record WebhookSubscriptionEventTypePersistenceModel
{
    public required Guid SubscriptionId { get; init; }

    public required string EventType { get; init; }
}

public sealed class PostgresWebhookSubscriptionStore : IWebhookSubscriptionStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresWebhookSubscriptionStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<WebhookSubscription>> ListAsync(
        WebhookSubscriptionListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var subscriptions = (await connection.QueryAsync<WebhookSubscriptionPersistenceModel>(new CommandDefinition(
            """
            select
                id as Id,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                endpoint_url as EndpointUrl,
                status as Status,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc
            from auth.webhook_subscriptions
            where (@TenantId is null or tenant_id = @TenantId)
              and (@ApplicationClientId is null or application_client_id = @ApplicationClientId)
            order by tenant_id, application_client_id, endpoint_url;
            """,
            new
            {
                request.TenantId,
                request.ApplicationClientId,
            },
            cancellationToken: cancellationToken)))
            .ToArray();
        if (subscriptions.Length == 0)
        {
            return Array.Empty<WebhookSubscription>();
        }

        var subscriptionIds = subscriptions.Select(static subscription => subscription.Id).ToArray();
        var eventTypes = (await connection.QueryAsync<WebhookSubscriptionEventTypePersistenceModel>(new CommandDefinition(
            """
            select
                subscription_id as SubscriptionId,
                event_type as EventType
            from auth.webhook_subscription_event_types
            where subscription_id = any(@SubscriptionIds)
            order by subscription_id, event_type;
            """,
            new { SubscriptionIds = subscriptionIds },
            cancellationToken: cancellationToken)))
            .ToArray();
        var eventTypesBySubscriptionId = eventTypes
            .GroupBy(static item => item.SubscriptionId)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyCollection<string>)group
                    .Select(static item => item.EventType)
                    .ToArray());

        return subscriptions
            .Select(subscription => new WebhookSubscription
            {
                SubscriptionId = subscription.Id,
                TenantId = subscription.TenantId,
                ApplicationClientId = subscription.ApplicationClientId,
                EndpointUrl = new Uri(subscription.EndpointUrl, UriKind.Absolute),
                IsActive = string.Equals(subscription.Status, "active", StringComparison.Ordinal),
                EventTypes = eventTypesBySubscriptionId.TryGetValue(subscription.Id, out var eventTypesForSubscription)
                    ? eventTypesForSubscription
                    : Array.Empty<string>(),
                CreatedUtc = subscription.CreatedUtc,
                UpdatedUtc = subscription.UpdatedUtc,
            })
            .ToArray();
    }

    public async Task<WebhookSubscription> UpsertAsync(
        WebhookSubscriptionUpsertRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var subscription = await connection.QuerySingleAsync<WebhookSubscriptionPersistenceModel>(new CommandDefinition(
            """
            insert into auth.webhook_subscriptions (
                id,
                tenant_id,
                application_client_id,
                endpoint_url,
                status,
                created_utc,
                updated_utc
            ) values (
                gen_random_uuid(),
                @TenantId,
                @ApplicationClientId,
                @EndpointUrl,
                @Status,
                timezone('utc', now()),
                timezone('utc', now())
            )
            on conflict (tenant_id, application_client_id, endpoint_url) do update
            set status = excluded.status,
                updated_utc = timezone('utc', now())
            returning
                id as Id,
                tenant_id as TenantId,
                application_client_id as ApplicationClientId,
                endpoint_url as EndpointUrl,
                status as Status,
                created_utc as CreatedUtc,
                updated_utc as UpdatedUtc;
            """,
            new
            {
                request.TenantId,
                request.ApplicationClientId,
                EndpointUrl = request.EndpointUrl.ToString(),
                Status = request.IsActive ? "active" : "inactive",
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            delete from auth.webhook_subscription_event_types
            where subscription_id = @SubscriptionId;
            """,
            new
            {
                SubscriptionId = subscription.Id,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        foreach (var eventType in request.EventTypes)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                insert into auth.webhook_subscription_event_types (
                    subscription_id,
                    event_type
                ) values (
                    @SubscriptionId,
                    @EventType
                );
                """,
                new
                {
                    SubscriptionId = subscription.Id,
                    EventType = eventType,
                },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);

        return new WebhookSubscription
        {
            SubscriptionId = subscription.Id,
            TenantId = subscription.TenantId,
            ApplicationClientId = subscription.ApplicationClientId,
            EndpointUrl = new Uri(subscription.EndpointUrl, UriKind.Absolute),
            IsActive = string.Equals(subscription.Status, "active", StringComparison.Ordinal),
            EventTypes = request.EventTypes.ToArray(),
            CreatedUtc = subscription.CreatedUtc,
            UpdatedUtc = subscription.UpdatedUtc,
        };
    }
}
