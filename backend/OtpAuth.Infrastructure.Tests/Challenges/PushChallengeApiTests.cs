using System.Net;
using System.Net.Http.Json;
using OtpAuth.Api.Challenges;
using OtpAuth.Api.Devices;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Devices;
using OtpAuth.Domain.Policy;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class PushChallengeApiTests
{
    [Fact]
    public async Task ListPending_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new PushChallengeApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/devices/me/challenges/pending");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListPending_ReturnsForbidden_WhenScopeIsMissing()
    {
        await using var seeded = CreateBoundFactory();
        using var client = seeded.Factory.CreateAuthorizedClient(PushChallengeApiTestFactory.MissingScopeScenario);

        var response = await client.GetAsync("/api/v1/devices/me/challenges/pending");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListPending_ReturnsOnlyPendingChallengesForAuthenticatedDevice()
    {
        await using var seeded = CreateBoundFactory();
        var factory = seeded.Factory;
        using var client = factory.CreateAuthorizedClient();

        var laterChallenge = new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = PushChallengeApiTestContext.TenantId,
            ApplicationClientId = PushChallengeApiTestContext.ApplicationClientId,
            ExternalUserId = "user-api",
            Username = "push.api",
            OperationType = OperationType.StepUp,
            OperationDisplayName = "Approve payroll step-up",
            FactorType = FactorType.Push,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(6),
            TargetDeviceId = PushChallengeApiTestContext.DeviceId,
            CorrelationId = "push-api-002",
        };
        await factory.GetChallengeRepository().AddAsync(laterChallenge, CancellationToken.None);
        await factory.GetChallengeRepository().AddAsync(
            laterChallenge with
            {
                Id = Guid.NewGuid(),
                TargetDeviceId = Guid.NewGuid(),
                CorrelationId = "push-api-other-device",
            },
            CancellationToken.None);
        await factory.GetChallengeRepository().AddAsync(
            laterChallenge with
            {
                Id = Guid.NewGuid(),
                Status = ChallengeStatus.Denied,
                DeniedUtc = DateTimeOffset.UtcNow,
                CorrelationId = "push-api-denied",
            },
            CancellationToken.None);
        await factory.GetChallengeRepository().AddAsync(
            laterChallenge with
            {
                Id = Guid.NewGuid(),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CorrelationId = "push-api-expired",
            },
            CancellationToken.None);

        var response = await client.GetAsync("/api/v1/devices/me/challenges/pending");
        var body = await response.Content.ReadFromJsonAsync<PendingDeviceChallengeHttpResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Collection(
            body!,
            first =>
            {
                Assert.Equal(seeded.SeededChallenge.Id, first.Id);
                Assert.Equal("push", first.FactorType);
                Assert.Equal("pending", first.Status);
                Assert.Equal("login", first.OperationType);
            },
            second =>
            {
                Assert.Equal(laterChallenge.Id, second.Id);
                Assert.Equal("step_up", second.OperationType);
                Assert.Equal("Approve payroll step-up", second.OperationDisplayName);
            });
    }

    [Fact]
    public async Task Approve_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new PushChallengeApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/challenges/{Guid.NewGuid()}/approve",
            new ApproveChallengeHttpRequest
            {
                DeviceId = PushChallengeApiTestContext.DeviceId,
                BiometricVerified = true,
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Approve_ReturnsForbidden_WhenScopeIsMissing()
    {
        await using var seeded = CreateBoundFactory();
        var factory = seeded.Factory;
        using var client = factory.CreateAuthorizedClient(PushChallengeApiTestFactory.MissingScopeScenario);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/challenges/{seeded.SeededChallenge.Id}/approve",
            new ApproveChallengeHttpRequest
            {
                DeviceId = PushChallengeApiTestContext.DeviceId,
                BiometricVerified = true,
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_ReturnsApprovedChallenge_WhenBindingMatches()
    {
        await using var seeded = CreateBoundFactory();
        var factory = seeded.Factory;
        var audit = factory.GetAuditWriter();
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/challenges/{seeded.SeededChallenge.Id}/approve",
            new ApproveChallengeHttpRequest
            {
                DeviceId = PushChallengeApiTestContext.DeviceId,
                BiometricVerified = true,
            });
        var body = await response.Content.ReadFromJsonAsync<ChallengeHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("approved", body!.Status);
        Assert.NotNull(body.ApprovedAt);
        Assert.Contains(audit.Events, entry => entry == $"approved:{seeded.SeededChallenge.Id}:{PushChallengeApiTestContext.DeviceId}:True");
    }

    [Fact]
    public async Task Approve_ReturnsNotFound_WhenChallengeIsBoundToDifferentDevice()
    {
        await using var seeded = CreateBoundFactory(boundDeviceId: Guid.NewGuid());
        var factory = seeded.Factory;
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/challenges/{seeded.SeededChallenge.Id}/approve",
            new ApproveChallengeHttpRequest
            {
                DeviceId = PushChallengeApiTestContext.DeviceId,
                BiometricVerified = true,
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deny_ReturnsDeniedChallenge_WhenBindingMatches()
    {
        await using var seeded = CreateBoundFactory();
        var factory = seeded.Factory;
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/challenges/{seeded.SeededChallenge.Id}/deny",
            new DenyChallengeHttpRequest
            {
                DeviceId = PushChallengeApiTestContext.DeviceId,
                Reason = "Unexpected sign-in",
            });
        var body = await response.Content.ReadFromJsonAsync<ChallengeHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("denied", body!.Status);
        Assert.NotNull(body.DeniedAt);
    }

    [Fact]
    public async Task Approve_ReturnsGone_WhenChallengeExpired()
    {
        await using var seeded = CreateBoundFactory(expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));
        var factory = seeded.Factory;
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/challenges/{seeded.SeededChallenge.Id}/approve",
            new ApproveChallengeHttpRequest
            {
                DeviceId = PushChallengeApiTestContext.DeviceId,
                BiometricVerified = true,
            });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    private static SeededPushChallengeFactory CreateBoundFactory(Guid? boundDeviceId = null, DateTimeOffset? expiresAt = null)
    {
        var factory = new PushChallengeApiTestFactory();
        var deviceStore = factory.GetDeviceStore();
        deviceStore.SeedActiveDevice(
            PushChallengeApiTestContext.TenantId,
            PushChallengeApiTestContext.ApplicationClientId,
            "user-api",
            "installation-api",
            pushToken: "push-token",
            deviceId: PushChallengeApiTestContext.DeviceId);

        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = PushChallengeApiTestContext.TenantId,
            ApplicationClientId = PushChallengeApiTestContext.ApplicationClientId,
            ExternalUserId = "user-api",
            Username = "push.api",
            OperationType = OperationType.Login,
            OperationDisplayName = "Approve sign in",
            FactorType = FactorType.Push,
            Status = ChallengeStatus.Pending,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5),
            TargetDeviceId = boundDeviceId ?? PushChallengeApiTestContext.DeviceId,
            CorrelationId = "push-api-001",
            CallbackUrl = new Uri("https://crm.example.com/webhooks/challenges"),
        };
        factory.GetChallengeRepository().AddAsync(challenge, CancellationToken.None).GetAwaiter().GetResult();

        return new SeededPushChallengeFactory(factory, challenge);
    }

    private sealed record SeededPushChallengeFactory(PushChallengeApiTestFactory Factory, Challenge SeededChallenge) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return Factory.DisposeAsync();
        }
    }
}
