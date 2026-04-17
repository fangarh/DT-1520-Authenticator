using OtpAuth.Application.Challenges;
using OtpAuth.Application.Devices;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Devices;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using OtpAuth.Infrastructure.Policy;
using OtpAuth.Infrastructure.Tests.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class PushChallengeDecisionHandlerTests
{
    [Fact]
    public async Task ApproveHandleAsync_ApprovesBoundPushChallenge_WhenDeviceAndBiometricMatch()
    {
        var repository = new InMemoryChallengeRepository();
        var deviceStore = new InMemoryDeviceRegistryStore();
        var device = deviceStore.SeedActiveDevice(Guid.NewGuid(), Guid.NewGuid(), "user-push", "installation-approve");
        var challenge = CreatePushChallenge(device.Device);
        await repository.AddAsync(challenge, CancellationToken.None);
        var attempts = new RecordingChallengeAttemptRecorder();
        var audit = new RecordingChallengeDecisionAuditWriter();
        var handler = new ApprovePushChallengeHandler(
            repository,
            attempts,
            audit,
            deviceStore,
            new DefaultPolicyEvaluator());

        var result = await handler.HandleAsync(
            new ApprovePushChallengeRequest
            {
                ChallengeId = challenge.Id,
                DeviceId = device.Device.Id,
                BiometricVerified = true,
            },
            CreateDeviceContext(device.Device),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Approved, result.Challenge!.Status);
        Assert.NotNull(result.Challenge.ApprovedUtc);
        Assert.Equal(ChallengeAttemptResults.Approved, Assert.Single(attempts.Attempts).Result);
        Assert.Contains(audit.Events, entry => entry == $"approved:{challenge.Id}:{device.Device.Id}:True");
    }

    [Fact]
    public async Task ApproveHandleAsync_ReturnsValidationFailure_WhenBiometricWasNotVerified()
    {
        var device = CreatePushChallengeSeed();
        var handler = new ApprovePushChallengeHandler(
            new InMemoryChallengeRepository(),
            new RecordingChallengeAttemptRecorder(),
            new RecordingChallengeDecisionAuditWriter(),
            device.Store,
            new DefaultPolicyEvaluator());

        var result = await handler.HandleAsync(
            new ApprovePushChallengeRequest
            {
                ChallengeId = device.Challenge.Id,
                DeviceId = device.Device.Device.Id,
                BiometricVerified = false,
            },
            CreateDeviceContext(device.Device.Device),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApprovePushChallengeErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("BiometricVerified must be true for push approval.", result.ErrorMessage);
    }

    [Fact]
    public async Task ApproveHandleAsync_ReturnsNotFound_WhenChallengeIsBoundToDifferentDevice()
    {
        var repository = new InMemoryChallengeRepository();
        var deviceStore = new InMemoryDeviceRegistryStore();
        var approvedDevice = deviceStore.SeedActiveDevice(Guid.NewGuid(), Guid.NewGuid(), "user-push", "installation-primary");
        var secondDevice = deviceStore.SeedActiveDevice(
            approvedDevice.Device.TenantId,
            approvedDevice.Device.ApplicationClientId,
            approvedDevice.Device.ExternalUserId,
            "installation-secondary");
        var challenge = CreatePushChallenge(approvedDevice.Device);
        await repository.AddAsync(challenge, CancellationToken.None);
        var handler = new ApprovePushChallengeHandler(
            repository,
            new RecordingChallengeAttemptRecorder(),
            new RecordingChallengeDecisionAuditWriter(),
            deviceStore,
            new DefaultPolicyEvaluator());

        var result = await handler.HandleAsync(
            new ApprovePushChallengeRequest
            {
                ChallengeId = challenge.Id,
                DeviceId = secondDevice.Device.Id,
                BiometricVerified = true,
            },
            CreateDeviceContext(secondDevice.Device),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApprovePushChallengeErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ApproveHandleAsync_ReturnsPolicyDenied_WhenDeviceHasNoPushToken()
    {
        var repository = new InMemoryChallengeRepository();
        var deviceStore = new InMemoryDeviceRegistryStore();
        var device = deviceStore.SeedActiveDevice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-policy",
            "installation-policy",
            pushToken: null);
        var challenge = CreatePushChallenge(device.Device);
        await repository.AddAsync(challenge, CancellationToken.None);
        var attempts = new RecordingChallengeAttemptRecorder();
        var handler = new ApprovePushChallengeHandler(
            repository,
            attempts,
            new RecordingChallengeDecisionAuditWriter(),
            deviceStore,
            new DefaultPolicyEvaluator());

        var result = await handler.HandleAsync(
            new ApprovePushChallengeRequest
            {
                ChallengeId = challenge.Id,
                DeviceId = device.Device.Id,
                BiometricVerified = true,
            },
            CreateDeviceContext(device.Device),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApprovePushChallengeErrorCode.PolicyDenied, result.ErrorCode);
        Assert.Equal(ChallengeAttemptResults.PolicyDenied, Assert.Single(attempts.Attempts).Result);
    }

    [Fact]
    public async Task DenyHandleAsync_DeniesBoundPushChallenge_WithoutPersistingRawReason()
    {
        var repository = new InMemoryChallengeRepository();
        var deviceStore = new InMemoryDeviceRegistryStore();
        var device = deviceStore.SeedActiveDevice(Guid.NewGuid(), Guid.NewGuid(), "user-deny", "installation-deny");
        var challenge = CreatePushChallenge(device.Device);
        await repository.AddAsync(challenge, CancellationToken.None);
        var attempts = new RecordingChallengeAttemptRecorder();
        var audit = new RecordingChallengeDecisionAuditWriter();
        var handler = new DenyPushChallengeHandler(repository, attempts, audit, deviceStore);

        var result = await handler.HandleAsync(
            new DenyPushChallengeRequest
            {
                ChallengeId = challenge.Id,
                DeviceId = device.Device.Id,
                Reason = "This was not me",
            },
            CreateDeviceContext(device.Device),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Denied, result.Challenge!.Status);
        Assert.NotNull(result.Challenge.DeniedUtc);
        Assert.Equal(ChallengeAttemptResults.Denied, Assert.Single(attempts.Attempts).Result);
        Assert.Contains(audit.Events, entry => entry == $"denied:{challenge.Id}:{device.Device.Id}:True");
    }

    [Fact]
    public async Task DenyHandleAsync_ExpiresPendingChallenge_WhenLifetimeElapsed()
    {
        var repository = new InMemoryChallengeRepository();
        var deviceStore = new InMemoryDeviceRegistryStore();
        var device = deviceStore.SeedActiveDevice(Guid.NewGuid(), Guid.NewGuid(), "user-expired", "installation-expired");
        var challenge = CreatePushChallenge(device.Device) with
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1),
        };
        await repository.AddAsync(challenge, CancellationToken.None);
        var attempts = new RecordingChallengeAttemptRecorder();
        var handler = new DenyPushChallengeHandler(
            repository,
            attempts,
            new RecordingChallengeDecisionAuditWriter(),
            deviceStore);

        var result = await handler.HandleAsync(
            new DenyPushChallengeRequest
            {
                ChallengeId = challenge.Id,
            },
            CreateDeviceContext(device.Device),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DenyPushChallengeErrorCode.ChallengeExpired, result.ErrorCode);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Expired, result.Challenge!.Status);
        Assert.Equal(ChallengeAttemptResults.Expired, Assert.Single(attempts.Attempts).Result);
    }

    private static (InMemoryDeviceRegistryStore Store, InMemoryDeviceRegistryStore.SeededDevice Device, Challenge Challenge) CreatePushChallengeSeed()
    {
        var store = new InMemoryDeviceRegistryStore();
        var device = store.SeedActiveDevice(Guid.NewGuid(), Guid.NewGuid(), "user-push", "installation");
        return (store, device, CreatePushChallenge(device.Device));
    }

    private static Challenge CreatePushChallenge(RegisteredDevice device)
    {
        return new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = device.TenantId,
            ApplicationClientId = device.ApplicationClientId,
            ExternalUserId = device.ExternalUserId,
            Username = "push.user",
            OperationType = OperationType.Login,
            OperationDisplayName = "Approve sign in",
            FactorType = FactorType.Push,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            TargetDeviceId = device.Id,
            CorrelationId = "push-req-001",
            CallbackUrl = new Uri("https://crm.example.com/webhooks/challenges"),
        };
    }

    private static DeviceClientContext CreateDeviceContext(RegisteredDevice device)
    {
        return new DeviceClientContext
        {
            DeviceId = device.Id,
            TenantId = device.TenantId,
            ApplicationClientId = device.ApplicationClientId,
            Scopes = [DeviceTokenScope.Challenge],
        };
    }

}
