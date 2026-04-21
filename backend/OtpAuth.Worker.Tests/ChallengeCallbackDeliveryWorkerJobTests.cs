using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Observability;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using Xunit;

namespace OtpAuth.Worker.Tests;

public sealed class ChallengeCallbackDeliveryWorkerJobTests
{
    [Fact]
    public async Task ExecuteAsync_MapsCoordinatorResultToSanitizedMetrics()
    {
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-callback",
            Username = "user.callback",
            OperationType = OperationType.Login,
            OperationDisplayName = "Approve login",
            FactorType = FactorType.Push,
            Status = ChallengeStatus.Approved,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            ApprovedUtc = DateTimeOffset.UtcNow,
            CorrelationId = "callback-corr-001",
            CallbackUrl = new Uri("https://crm.example.com/webhooks/otpauth"),
        };
        var delivery = ChallengeCallbackDelivery.CreateQueued(
            challenge,
            ChallengeCallbackEventType.Approved,
            challenge.ApprovedUtc!.Value);
        var store = new StubCallbackStore(delivery);
        var coordinator = new ChallengeCallbackDeliveryCoordinator(
            new StubChallengeRepository(challenge),
            new StubCallbackGateway(),
            store);
        var job = new ChallengeCallbackDeliveryWorkerJob(
            coordinator,
            store,
            Options.Create(new ChallengeCallbackDeliveryWorkerJobOptions
            {
                Enabled = true,
                IntervalSeconds = 15,
                BatchSize = 20,
                LeaseSeconds = 60,
                RetryDelaySeconds = 30,
                MaxAttempts = 5,
            }),
            NullLogger<ChallengeCallbackDeliveryWorkerJob>.Instance);

        var result = await job.ExecuteAsync(new DateTimeOffset(2026, 04, 20, 13, 00, 00, TimeSpan.Zero), CancellationToken.None);

        Assert.Equal("challenge_callback_delivery_cycle_completed", result.Summary);
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
                Assert.Equal(3, metric.Value);
            },
            metric =>
            {
                Assert.Equal("retrying", metric.Name);
                Assert.Equal(1, metric.Value);
            },
            metric =>
            {
                Assert.Equal("deliveredTotal", metric.Name);
                Assert.Equal(8, metric.Value);
            },
            metric =>
            {
                Assert.Equal("failedTotal", metric.Name);
                Assert.Equal(2, metric.Value);
            });
    }

    [Fact]
    public void GetInterval_RejectsNonPositiveValue()
    {
        var options = new ChallengeCallbackDeliveryWorkerJobOptions
        {
            IntervalSeconds = 0,
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.GetInterval());

        Assert.Equal(
            "WorkerJobs:ChallengeCallbackDelivery:IntervalSeconds must be a positive number of seconds.",
            exception.Message);
    }

    private sealed class StubChallengeRepository(Challenge challenge) : IChallengeRepository
    {
        public Task AddAsync(Challenge challenge, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAsync(Challenge challenge, PushChallengeDelivery? pushDelivery, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyCollection<Challenge>> ListPendingPushByTargetDeviceAsync(
            Guid targetDeviceId,
            Guid tenantId,
            Guid applicationClientId,
            DateTimeOffset utcNow,
            int maxResults,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<Challenge> challenges = [challenge];
            return Task.FromResult(challenges);
        }

        public Task<Challenge?> GetByIdAsync(
            Guid challengeId,
            Guid tenantId,
            Guid applicationClientId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Challenge?>(challenge);
        }

        public Task UpdateAsync(Challenge challenge, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(
            Challenge challenge,
            ChallengeUpdateSideEffects? sideEffects,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubCallbackGateway : IChallengeCallbackDeliveryGateway
    {
        public Task<ChallengeCallbackDispatchResult> DeliverAsync(
            ChallengeCallbackDispatchRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ChallengeCallbackDispatchResult.Success());
        }
    }

    private sealed class StubCallbackStore(ChallengeCallbackDelivery delivery) : IChallengeCallbackDeliveryStore
    {
        public Task<IReadOnlyCollection<ChallengeCallbackDelivery>> LeaseDueAsync(
            DateTimeOffset utcNow,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ChallengeCallbackDelivery> leased =
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
                QueuedCount = 3,
                DeliveredCount = 8,
                FailedCount = 2,
                RetryingCount = 1,
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
