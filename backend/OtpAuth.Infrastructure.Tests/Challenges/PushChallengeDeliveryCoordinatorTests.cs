using OtpAuth.Application.Challenges;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using OtpAuth.Infrastructure.Tests.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class PushChallengeDeliveryCoordinatorTests
{
    [Fact]
    public async Task DeliverDueAsync_MarksDeliveryDelivered_WhenGatewaySucceeds()
    {
        var repository = new InMemoryChallengeRepository();
        var devices = new InMemoryDeviceRegistryStore();
        var device = devices.SeedActiveDevice(Guid.NewGuid(), Guid.NewGuid(), "user-push", "installation-1");
        var challenge = CreatePushChallenge(device);
        await repository.AddAsync(
            challenge,
            PushChallengeDelivery.CreateQueued(
                challenge.Id,
                challenge.TenantId,
                challenge.ApplicationClientId,
                challenge.ExternalUserId,
                device.Device.Id,
                DateTimeOffset.UtcNow),
            CancellationToken.None);
        var store = new StubPushChallengeDeliveryStore(repository.GetPushDeliveries());
        var gateway = new StubPushChallengeDeliveryGateway(PushChallengeDispatchResult.Success("provider-1"));
        var coordinator = new PushChallengeDeliveryCoordinator(repository, devices, gateway, store);

        var result = await coordinator.DeliverDueAsync(
            DateTimeOffset.UtcNow,
            batchSize: 10,
            leaseDuration: TimeSpan.FromMinutes(1),
            retryDelay: TimeSpan.FromSeconds(30),
            maxAttempts: 5,
            CancellationToken.None);

        Assert.Equal(1, result.LeasedCount);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Equal(0, result.RescheduledCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Single(store.Delivered);
    }

    [Fact]
    public async Task DeliverDueAsync_ReschedulesDelivery_WhenGatewayReturnsRetryableFailure()
    {
        var repository = new InMemoryChallengeRepository();
        var devices = new InMemoryDeviceRegistryStore();
        var device = devices.SeedActiveDevice(Guid.NewGuid(), Guid.NewGuid(), "user-push", "installation-2");
        var challenge = CreatePushChallenge(device);
        await repository.AddAsync(
            challenge,
            PushChallengeDelivery.CreateQueued(
                challenge.Id,
                challenge.TenantId,
                challenge.ApplicationClientId,
                challenge.ExternalUserId,
                device.Device.Id,
                DateTimeOffset.UtcNow),
            CancellationToken.None);
        var store = new StubPushChallengeDeliveryStore(repository.GetPushDeliveries());
        var gateway = new StubPushChallengeDeliveryGateway(PushChallengeDispatchResult.Failure("provider_unavailable", isRetryable: true));
        var coordinator = new PushChallengeDeliveryCoordinator(repository, devices, gateway, store);

        var result = await coordinator.DeliverDueAsync(
            DateTimeOffset.UtcNow,
            batchSize: 10,
            leaseDuration: TimeSpan.FromMinutes(1),
            retryDelay: TimeSpan.FromSeconds(30),
            maxAttempts: 5,
            CancellationToken.None);

        Assert.Equal(1, result.RescheduledCount);
        Assert.Single(store.Rescheduled);
        Assert.Equal("provider_unavailable", store.Rescheduled[0].ErrorCode);
    }

    [Fact]
    public async Task DeliverDueAsync_FailsDelivery_WhenDeviceNoLongerHasPushChannel()
    {
        var repository = new InMemoryChallengeRepository();
        var devices = new InMemoryDeviceRegistryStore();
        var device = devices.SeedActiveDevice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-push",
            "installation-3",
            pushToken: null);
        var challenge = CreatePushChallenge(device);
        await repository.AddAsync(
            challenge,
            PushChallengeDelivery.CreateQueued(
                challenge.Id,
                challenge.TenantId,
                challenge.ApplicationClientId,
                challenge.ExternalUserId,
                device.Device.Id,
                DateTimeOffset.UtcNow),
            CancellationToken.None);
        var store = new StubPushChallengeDeliveryStore(repository.GetPushDeliveries());
        var gateway = new StubPushChallengeDeliveryGateway(PushChallengeDispatchResult.Success());
        var coordinator = new PushChallengeDeliveryCoordinator(repository, devices, gateway, store);

        var result = await coordinator.DeliverDueAsync(
            DateTimeOffset.UtcNow,
            batchSize: 10,
            leaseDuration: TimeSpan.FromMinutes(1),
            retryDelay: TimeSpan.FromSeconds(30),
            maxAttempts: 5,
            CancellationToken.None);

        Assert.Equal(1, result.FailedCount);
        Assert.Single(store.Failed);
        Assert.Equal("device_unavailable", store.Failed[0].ErrorCode);
    }

    private static Challenge CreatePushChallenge(InMemoryDeviceRegistryStore.SeededDevice device)
    {
        return new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = device.Device.TenantId,
            ApplicationClientId = device.Device.ApplicationClientId,
            ExternalUserId = device.Device.ExternalUserId,
            Username = "user.push",
            OperationType = OperationType.Login,
            OperationDisplayName = "Approve login",
            FactorType = FactorType.Push,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            TargetDeviceId = device.Device.Id,
            CorrelationId = "push-corr-001",
        };
    }

    private sealed class StubPushChallengeDeliveryGateway(PushChallengeDispatchResult result) : IPushChallengeDeliveryGateway
    {
        public Task<PushChallengeDispatchResult> DeliverAsync(
            PushChallengeDispatchRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class StubPushChallengeDeliveryStore(IReadOnlyCollection<PushChallengeDelivery> deliveries) : IPushChallengeDeliveryStore
    {
        public List<Guid> Delivered { get; } = [];

        public List<(Guid DeliveryId, DateTimeOffset NextAttemptUtc, string ErrorCode)> Rescheduled { get; } = [];

        public List<(Guid DeliveryId, string ErrorCode)> Failed { get; } = [];

        public Task<IReadOnlyCollection<PushChallengeDelivery>> LeaseDueAsync(
            DateTimeOffset utcNow,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<PushChallengeDelivery> leased = deliveries
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

        public Task MarkDeliveredAsync(
            Guid deliveryId,
            DateTimeOffset deliveredAtUtc,
            string? providerMessageId,
            CancellationToken cancellationToken)
        {
            Delivered.Add(deliveryId);
            return Task.CompletedTask;
        }

        public Task RescheduleAsync(
            Guid deliveryId,
            DateTimeOffset nextAttemptUtc,
            string errorCode,
            CancellationToken cancellationToken)
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
