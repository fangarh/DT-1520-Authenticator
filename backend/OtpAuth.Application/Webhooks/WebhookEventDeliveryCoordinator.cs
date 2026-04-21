namespace OtpAuth.Application.Webhooks;

public sealed record WebhookEventDeliveryBatchResult
{
    public required int LeasedCount { get; init; }

    public required int DeliveredCount { get; init; }

    public required int RescheduledCount { get; init; }

    public required int FailedCount { get; init; }
}

public sealed class WebhookEventDeliveryCoordinator
{
    private readonly IWebhookEventDeliveryGateway _gateway;
    private readonly IWebhookEventDeliveryStore _store;

    public WebhookEventDeliveryCoordinator(
        IWebhookEventDeliveryGateway gateway,
        IWebhookEventDeliveryStore store)
    {
        _gateway = gateway;
        _store = store;
    }

    public async Task<WebhookEventDeliveryBatchResult> DeliverDueAsync(
        DateTimeOffset utcNow,
        int batchSize,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var leasedDeliveries = await _store.LeaseDueAsync(
            utcNow,
            batchSize,
            leaseDuration,
            cancellationToken);
        var deliveredCount = 0;
        var rescheduledCount = 0;
        var failedCount = 0;

        foreach (var delivery in leasedDeliveries)
        {
            var dispatchResult = await _gateway.DeliverAsync(
                new WebhookEventDispatchRequest
                {
                    DeliveryId = delivery.DeliveryId,
                    EventId = delivery.EventId,
                    EventType = delivery.EventType,
                    EndpointUrl = delivery.EndpointUrl,
                    PayloadJson = delivery.PayloadJson,
                },
                cancellationToken);

            if (dispatchResult.IsSuccess)
            {
                await _store.MarkDeliveredAsync(delivery.DeliveryId, utcNow, cancellationToken);
                deliveredCount++;
                continue;
            }

            if (dispatchResult.IsRetryable && delivery.AttemptCount < maxAttempts)
            {
                await _store.RescheduleAsync(
                    delivery.DeliveryId,
                    utcNow.Add(retryDelay),
                    dispatchResult.ErrorCode ?? "delivery_failed",
                    cancellationToken);
                rescheduledCount++;
                continue;
            }

            await _store.MarkFailedAsync(
                delivery.DeliveryId,
                dispatchResult.ErrorCode ?? "delivery_failed",
                cancellationToken);
            failedCount++;
        }

        return new WebhookEventDeliveryBatchResult
        {
            LeasedCount = leasedDeliveries.Count,
            DeliveredCount = deliveredCount,
            RescheduledCount = rescheduledCount,
            FailedCount = failedCount,
        };
    }
}
