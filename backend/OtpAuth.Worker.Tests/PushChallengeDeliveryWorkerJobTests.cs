using Microsoft.Extensions.Options;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Devices;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Devices;
using OtpAuth.Domain.Policy;
using Xunit;

namespace OtpAuth.Worker.Tests;

public sealed class PushChallengeDeliveryWorkerJobTests
{
    [Fact]
    public async Task ExecuteAsync_MapsCoordinatorResultToSanitizedMetrics()
    {
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-push",
            Username = "user.push",
            OperationType = OperationType.Login,
            OperationDisplayName = "Approve login",
            FactorType = FactorType.Push,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            TargetDeviceId = Guid.NewGuid(),
            CorrelationId = "push-corr-001",
        };
        var device = RegisteredDevice.Activate(
            challenge.TargetDeviceId!.Value,
            challenge.TenantId,
            challenge.ApplicationClientId,
            challenge.ExternalUserId,
            DevicePlatform.Android,
            "installation-1",
            "Pixel",
            "push-token",
            null,
            DateTimeOffset.UtcNow);
        var coordinator = new PushChallengeDeliveryCoordinator(
            new StubChallengeRepository(challenge),
            new StubDeviceRegistryStore(device),
            new StubPushChallengeDeliveryGateway(),
            new StubPushChallengeDeliveryStore(
                PushChallengeDelivery.CreateQueued(
                    challenge.Id,
                    challenge.TenantId,
                    challenge.ApplicationClientId,
                    challenge.ExternalUserId,
                    device.Id,
                    DateTimeOffset.UtcNow)));
        var job = new PushChallengeDeliveryWorkerJob(
            coordinator,
            Options.Create(new PushChallengeDeliveryWorkerJobOptions
            {
                Enabled = true,
                IntervalSeconds = 15,
                BatchSize = 20,
                LeaseSeconds = 60,
                RetryDelaySeconds = 30,
                MaxAttempts = 5,
            }));

        var result = await job.ExecuteAsync(new DateTimeOffset(2026, 04, 17, 12, 00, 00, TimeSpan.Zero), CancellationToken.None);

        Assert.Equal("push_delivery_cycle_completed", result.Summary);
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
            });
    }

    [Fact]
    public void GetInterval_RejectsNonPositiveValue()
    {
        var options = new PushChallengeDeliveryWorkerJobOptions
        {
            IntervalSeconds = 0
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.GetInterval());

        Assert.Equal(
            "WorkerJobs:PushChallengeDelivery:IntervalSeconds must be a positive number of seconds.",
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

    private sealed class StubDeviceRegistryStore(RegisteredDevice device) : IDeviceRegistryStore
    {
        public Task<DeviceActivationCodeArtifact?> GetActivationCodeByIdAsync(Guid activationCodeId, CancellationToken cancellationToken) => Task.FromResult<DeviceActivationCodeArtifact?>(null);

        public Task<RegisteredDevice?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken) => Task.FromResult<RegisteredDevice?>(device);

        public Task<RegisteredDevice?> GetByIdAsync(Guid deviceId, Guid tenantId, Guid applicationClientId, CancellationToken cancellationToken) => Task.FromResult<RegisteredDevice?>(device);

        public Task<RegisteredDevice?> GetActiveByInstallationAsync(Guid tenantId, Guid applicationClientId, string installationId, CancellationToken cancellationToken) => Task.FromResult<RegisteredDevice?>(device);

        public Task<IReadOnlyCollection<RegisteredDevice>> ListActiveByExternalUserAsync(Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<RegisteredDevice>>([device]);

        public Task<DeviceRefreshTokenRecord?> GetRefreshTokenByIdAsync(Guid tokenId, CancellationToken cancellationToken) => Task.FromResult<DeviceRefreshTokenRecord?>(null);

        public Task<bool> ActivateAsync(
            RegisteredDevice device,
            DeviceRefreshTokenRecord refreshToken,
            Guid activationCodeId,
            DateTimeOffset activatedAtUtc,
            DeviceLifecycleSideEffects? sideEffects,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> RotateRefreshTokenAsync(DeviceRefreshRotation rotation, Guid deviceId, DateTimeOffset lastSeenUtc, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> RevokeDeviceAsync(
            RegisteredDevice device,
            DateTimeOffset revokedAtUtc,
            DeviceLifecycleSideEffects? sideEffects,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> BlockDeviceAsync(
            RegisteredDevice device,
            DateTimeOffset blockedAtUtc,
            DeviceLifecycleSideEffects? sideEffects,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StubPushChallengeDeliveryGateway : IPushChallengeDeliveryGateway
    {
        public Task<PushChallengeDispatchResult> DeliverAsync(PushChallengeDispatchRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(PushChallengeDispatchResult.Success("provider-1"));
        }
    }

    private sealed class StubPushChallengeDeliveryStore(PushChallengeDelivery delivery) : IPushChallengeDeliveryStore
    {
        public Task<IReadOnlyCollection<PushChallengeDelivery>> LeaseDueAsync(
            DateTimeOffset utcNow,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<PushChallengeDelivery> leased =
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

        public Task MarkDeliveredAsync(Guid deliveryId, DateTimeOffset deliveredAtUtc, string? providerMessageId, CancellationToken cancellationToken)
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
