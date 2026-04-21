using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OtpAuth.Application.Observability;
using OtpAuth.Application.Webhooks;
using Xunit;

namespace OtpAuth.Worker.Tests;

public sealed class WebhookEventDeliveryWorkerJobTests
{
    [Fact]
    public async Task ExecuteAsync_MapsCoordinatorResultToSanitizedMetrics()
    {
        var store = new StubWebhookStore(CreateDelivery());
        var coordinator = new WebhookEventDeliveryCoordinator(
            new StubWebhookGateway(),
            store);
        var job = new WebhookEventDeliveryWorkerJob(
            coordinator,
            store,
            Options.Create(new WebhookEventDeliveryWorkerJobOptions
            {
                Enabled = true,
                IntervalSeconds = 15,
                BatchSize = 20,
                LeaseSeconds = 60,
                RetryDelaySeconds = 30,
                MaxAttempts = 5,
            }),
            NullLogger<WebhookEventDeliveryWorkerJob>.Instance);

        var result = await job.ExecuteAsync(new DateTimeOffset(2026, 04, 20, 13, 00, 00, TimeSpan.Zero), CancellationToken.None);

        Assert.Equal("webhook_event_delivery_cycle_completed", result.Summary);
        Assert.Collection(
            result.Metrics,
            metric =>
            {
                Assert.Equal("leased", metric.Name);
                Assert.Equal(1, metric.Value);
            },
            metric =>
            {
                Assert.Equal("delivered", metric.Name);
                Assert.Equal(1, metric.Value);
            },
            metric =>
            {
                Assert.Equal("rescheduled", metric.Name);
                Assert.Equal(0, metric.Value);
            },
            metric =>
            {
                Assert.Equal("failed", metric.Name);
                Assert.Equal(0, metric.Value);
            },
            metric =>
            {
                Assert.Equal("queued", metric.Name);
                Assert.Equal(5, metric.Value);
            },
            metric =>
            {
                Assert.Equal("retrying", metric.Name);
                Assert.Equal(2, metric.Value);
            },
            metric =>
            {
                Assert.Equal("deliveredTotal", metric.Name);
                Assert.Equal(11, metric.Value);
            },
            metric =>
            {
                Assert.Equal("failedTotal", metric.Name);
                Assert.Equal(4, metric.Value);
            });
    }

    [Fact]
    public void GetInterval_RejectsNonPositiveValue()
    {
        var options = new WebhookEventDeliveryWorkerJobOptions
        {
            IntervalSeconds = 0,
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.GetInterval());

        Assert.Equal(
            "WorkerJobs:WebhookEventDelivery:IntervalSeconds must be a positive number of seconds.",
            exception.Message);
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
            AttemptCount = 0,
            NextAttemptUtc = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
    }

    private sealed class StubWebhookGateway : IWebhookEventDeliveryGateway
    {
        public Task<WebhookEventDispatchResult> DeliverAsync(
            WebhookEventDispatchRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(WebhookEventDispatchResult.Success());
        }
    }

    private sealed class StubWebhookStore(WebhookEventDelivery delivery) : IWebhookEventDeliveryStore
    {
        public Task<IReadOnlyCollection<WebhookEventDelivery>> LeaseDueAsync(
            DateTimeOffset utcNow,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<WebhookEventDelivery> leased =
            [
                delivery with
                {
                    AttemptCount = delivery.AttemptCount + 1,
                    LastAttemptUtc = utcNow,
                    LockedUntilUtc = utcNow.Add(leaseDuration),
                }
            ];

            return Task.FromResult(leased);
        }

        public Task<DeliveryStatusMetricsSummary> GetStatusMetricsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DeliveryStatusMetricsSummary
            {
                QueuedCount = 5,
                DeliveredCount = 11,
                FailedCount = 4,
                RetryingCount = 2,
            });
        }

        public Task MarkDeliveredAsync(Guid deliveryId, DateTimeOffset deliveredAtUtc, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RescheduleAsync(Guid deliveryId, DateTimeOffset nextAttemptUtc, string errorCode, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid deliveryId, string errorCode, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
