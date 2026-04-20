using OtpAuth.Application.Challenges;
using OtpAuth.Application.Devices;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class ListPendingPushChallengesForDeviceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsOnlyPendingPushChallengesBoundToAuthenticatedDevice()
    {
        var repository = new InMemoryChallengeRepository();
        var tenantId = Guid.NewGuid();
        var applicationClientId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var otherDeviceId = Guid.NewGuid();

        var earliest = CreatePushChallenge(
            tenantId,
            applicationClientId,
            deviceId,
            "Approve payroll sign-in",
            DateTimeOffset.UtcNow.AddMinutes(2));
        var latest = CreatePushChallenge(
            tenantId,
            applicationClientId,
            deviceId,
            "Approve CRM sign-in",
            DateTimeOffset.UtcNow.AddMinutes(5));

        await repository.AddAsync(latest, CancellationToken.None);
        await repository.AddAsync(earliest, CancellationToken.None);
        await repository.AddAsync(
            CreatePushChallenge(
                tenantId,
                applicationClientId,
                otherDeviceId,
                "Other device",
                DateTimeOffset.UtcNow.AddMinutes(1)),
            CancellationToken.None);
        await repository.AddAsync(
            CreatePushChallenge(
                tenantId,
                applicationClientId,
                deviceId,
                "Expired",
                DateTimeOffset.UtcNow.AddMinutes(-1)),
            CancellationToken.None);
        await repository.AddAsync(
            CreatePushChallenge(
                tenantId,
                applicationClientId,
                deviceId,
                "Denied",
                DateTimeOffset.UtcNow.AddMinutes(3)) with
            {
                Status = ChallengeStatus.Denied,
                DeniedUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);
        await repository.AddAsync(
            CreatePushChallenge(
                tenantId,
                applicationClientId,
                deviceId,
                "Totp",
                DateTimeOffset.UtcNow.AddMinutes(3)) with
            {
                FactorType = FactorType.Totp,
                TargetDeviceId = null,
            },
            CancellationToken.None);

        var handler = new ListPendingPushChallengesForDeviceHandler(repository);
        var result = await handler.HandleAsync(
            CreateDeviceContext(deviceId, tenantId, applicationClientId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Challenges,
            first => Assert.Equal(earliest.Id, first.Id),
            second => Assert.Equal(latest.Id, second.Id));
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenScopeIsMissing()
    {
        var handler = new ListPendingPushChallengesForDeviceHandler(new InMemoryChallengeRepository());

        var result = await handler.HandleAsync(
            CreateDeviceContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Array.Empty<string>()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ListPendingPushChallengesForDeviceErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Scope 'device:challenge' is required.", result.ErrorMessage);
    }

    private static Challenge CreatePushChallenge(
        Guid tenantId,
        Guid applicationClientId,
        Guid deviceId,
        string operationDisplayName,
        DateTimeOffset expiresAt)
    {
        return new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationClientId = applicationClientId,
            ExternalUserId = "user-push",
            Username = "push.user",
            OperationType = OperationType.Login,
            OperationDisplayName = operationDisplayName,
            FactorType = FactorType.Push,
            Status = ChallengeStatus.Pending,
            ExpiresAt = expiresAt,
            TargetDeviceId = deviceId,
            CorrelationId = $"corr-{Guid.NewGuid():N}",
        };
    }

    private static DeviceClientContext CreateDeviceContext(
        Guid deviceId,
        Guid tenantId,
        Guid applicationClientId,
        IReadOnlyCollection<string>? scopes = null)
    {
        return new DeviceClientContext
        {
            DeviceId = deviceId,
            TenantId = tenantId,
            ApplicationClientId = applicationClientId,
            Scopes = scopes ?? [DeviceTokenScope.Challenge],
        };
    }
}
