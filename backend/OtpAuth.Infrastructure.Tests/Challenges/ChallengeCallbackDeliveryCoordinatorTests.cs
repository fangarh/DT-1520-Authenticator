using OtpAuth.Application.Challenges;
using OtpAuth.Application.Observability;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class ChallengeCallbackDeliveryCoordinatorTests
{
    [Fact]
    public async Task DeliverDueAsync_MarksDeliveryDelivered_WhenGatewaySucceeds()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge() with
        {
            Status = ChallengeStatus.Approved,
            ApprovedUtc = DateTimeOffset.UtcNow,
        };
        await repository.AddAsync(challenge, CancellationToken.None);
        var delivery = ChallengeCallbackDelivery.CreateQueued(
            challenge,
            ChallengeCallbackEventType.Approved,
            challenge.ApprovedUtc!.Value);
        await repository.UpdateAsync(
            challenge,
            new ChallengeUpdateSideEffects
            {
                CallbackDelivery = delivery,
            },
            CancellationToken.None);
        var store = new StubStore(repository.GetCallbackDeliveries());
        var coordinator = new ChallengeCallbackDeliveryCoordinator(
            repository,
            new StubGateway(ChallengeCallbackDispatchResult.Success()),
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
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge() with
        {
            Status = ChallengeStatus.Denied,
            DeniedUtc = DateTimeOffset.UtcNow,
        };
        await repository.AddAsync(challenge, CancellationToken.None);
        var delivery = ChallengeCallbackDelivery.CreateQueued(
            challenge,
            ChallengeCallbackEventType.Denied,
            challenge.DeniedUtc!.Value);
        await repository.UpdateAsync(
            challenge,
            new ChallengeUpdateSideEffects
            {
                CallbackDelivery = delivery,
            },
            CancellationToken.None);
        var store = new StubStore(repository.GetCallbackDeliveries());
        var coordinator = new ChallengeCallbackDeliveryCoordinator(
            repository,
            new StubGateway(ChallengeCallbackDispatchResult.Failure("callback_timeout", isRetryable: true)),
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
        Assert.Equal("callback_timeout", store.Rescheduled[0].ErrorCode);
    }

    [Fact]
    public async Task DeliverDueAsync_FailsDelivery_WhenChallengeStateNoLongerMatchesEvent()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge() with
        {
            Status = ChallengeStatus.Pending,
        };
        await repository.AddAsync(challenge, CancellationToken.None);
        var delivery = ChallengeCallbackDelivery.CreateQueued(
            challenge with
            {
                Status = ChallengeStatus.Approved,
                ApprovedUtc = DateTimeOffset.UtcNow,
            },
            ChallengeCallbackEventType.Approved,
            DateTimeOffset.UtcNow);
        await repository.UpdateAsync(
            challenge,
            new ChallengeUpdateSideEffects
            {
                CallbackDelivery = delivery,
            },
            CancellationToken.None);
        var store = new StubStore(repository.GetCallbackDeliveries());
        var coordinator = new ChallengeCallbackDeliveryCoordinator(
            repository,
            new StubGateway(ChallengeCallbackDispatchResult.Success()),
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
        Assert.Equal("challenge_invalid", store.Failed[0].ErrorCode);
    }

    private static Challenge CreateChallenge()
    {
        return new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-callback",
            Username = "user.callback",
            OperationType = OperationType.Login,
            OperationDisplayName = "Approve login",
            FactorType = FactorType.Push,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CorrelationId = "callback-corr-001",
            CallbackUrl = new Uri("https://crm.example.com/webhooks/otpauth"),
        };
    }

    private sealed class StubGateway(ChallengeCallbackDispatchResult result) : IChallengeCallbackDeliveryGateway
    {
        public Task<ChallengeCallbackDispatchResult> DeliverAsync(
            ChallengeCallbackDispatchRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class StubStore(IReadOnlyCollection<ChallengeCallbackDelivery> deliveries) : IChallengeCallbackDeliveryStore
    {
        public List<Guid> Delivered { get; } = [];

        public List<(Guid DeliveryId, DateTimeOffset NextAttemptUtc, string ErrorCode)> Rescheduled { get; } = [];

        public List<(Guid DeliveryId, string ErrorCode)> Failed { get; } = [];

        public Task<IReadOnlyCollection<ChallengeCallbackDelivery>> LeaseDueAsync(
            DateTimeOffset utcNow,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ChallengeCallbackDelivery> leased = deliveries
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
