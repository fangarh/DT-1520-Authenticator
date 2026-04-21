using OtpAuth.Application.Webhooks;
using OtpAuth.Application.Observability;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Webhooks;

public sealed class WebhookEventDeliveryCoordinatorTests
{
    [Fact]
    public async Task DeliverDueAsync_MarksDeliveryDelivered_WhenGatewaySucceeds()
    {
        var delivery = CreateDelivery();
        var store = new StubStore([delivery]);
        var coordinator = new WebhookEventDeliveryCoordinator(
            new StubGateway(WebhookEventDispatchResult.Success()),
            store);

        var result = await coordinator.DeliverDueAsync(
            DateTimeOffset.UtcNow,
            10,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30),
            5,
            CancellationToken.None);

        Assert.Equal(1, result.LeasedCount);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Single(store.Delivered);
    }

    [Fact]
    public async Task DeliverDueAsync_ReschedulesDelivery_WhenGatewayReturnsRetryableFailure()
    {
        var delivery = CreateDelivery();
        var store = new StubStore([delivery]);
        var coordinator = new WebhookEventDeliveryCoordinator(
            new StubGateway(WebhookEventDispatchResult.Failure("webhook_timeout", isRetryable: true)),
            store);

        var result = await coordinator.DeliverDueAsync(
            DateTimeOffset.UtcNow,
            10,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30),
            5,
            CancellationToken.None);

        Assert.Equal(1, result.RescheduledCount);
        Assert.Single(store.Rescheduled);
        Assert.Equal("webhook_timeout", store.Rescheduled[0].ErrorCode);
    }

    [Fact]
    public async Task DeliverDueAsync_FailsDelivery_WhenGatewayReturnsNonRetryableFailure()
    {
        var delivery = CreateDelivery();
        var store = new StubStore([delivery]);
        var coordinator = new WebhookEventDeliveryCoordinator(
            new StubGateway(WebhookEventDispatchResult.Failure("webhook_rejected_bad_request", isRetryable: false)),
            store);

        var result = await coordinator.DeliverDueAsync(
            DateTimeOffset.UtcNow,
            10,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30),
            5,
            CancellationToken.None);

        Assert.Equal(1, result.FailedCount);
        Assert.Single(store.Failed);
        Assert.Equal("webhook_rejected_bad_request", store.Failed[0].ErrorCode);
    }

    private static WebhookEventDelivery CreateDelivery()
    {
        return new WebhookEventDelivery
        {
            DeliveryId = Guid.NewGuid(),
            SubscriptionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            EndpointUrl = new Uri("https://crm.example.com/webhooks/otpauth"),
            EventId = Guid.NewGuid(),
            EventType = WebhookEventTypeNames.ChallengeApproved,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ResourceType = WebhookResourceTypeNames.Challenge,
            ResourceId = Guid.NewGuid(),
            PayloadJson = """{"eventType":"challenge.approved"}""",
            Status = WebhookEventDeliveryStatus.Queued,
            AttemptCount = 1,
            NextAttemptUtc = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
    }

    private sealed class StubGateway(WebhookEventDispatchResult result) : IWebhookEventDeliveryGateway
    {
        public Task<WebhookEventDispatchResult> DeliverAsync(
            WebhookEventDispatchRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class StubStore(IReadOnlyCollection<WebhookEventDelivery> deliveries) : IWebhookEventDeliveryStore
    {
        public List<Guid> Delivered { get; } = [];

        public List<(Guid DeliveryId, DateTimeOffset NextAttemptUtc, string ErrorCode)> Rescheduled { get; } = [];

        public List<(Guid DeliveryId, string ErrorCode)> Failed { get; } = [];

        public Task<IReadOnlyCollection<WebhookEventDelivery>> LeaseDueAsync(
            DateTimeOffset utcNow,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<WebhookEventDelivery> leased = deliveries
                .Take(batchSize)
                .Select(delivery => delivery with
                {
                    AttemptCount = delivery.AttemptCount + 1,
                    LastAttemptUtc = utcNow,
                    LockedUntilUtc = utcNow.Add(leaseDuration),
                })
                .ToArray();

            return Task.FromResult(leased);
        }

        public Task MarkDeliveredAsync(Guid deliveryId, DateTimeOffset deliveredAtUtc, CancellationToken cancellationToken)
        {
            Delivered.Add(deliveryId);
            return Task.CompletedTask;
        }

        public Task<DeliveryStatusMetricsSummary> GetStatusMetricsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DeliveryStatusMetricsSummary
            {
                QueuedCount = deliveries.LongCount(),
                DeliveredCount = 0,
                FailedCount = 0,
                RetryingCount = deliveries.LongCount(delivery => delivery.AttemptCount > 0),
            });
        }

        public Task RescheduleAsync(Guid deliveryId, DateTimeOffset nextAttemptUtc, string errorCode, CancellationToken cancellationToken)
        {
            Rescheduled.Add((deliveryId, nextAttemptUtc, errorCode));
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid deliveryId, string errorCode, CancellationToken cancellationToken)
        {
            Failed.Add((deliveryId, errorCode));
            return Task.CompletedTask;
        }
    }
}
