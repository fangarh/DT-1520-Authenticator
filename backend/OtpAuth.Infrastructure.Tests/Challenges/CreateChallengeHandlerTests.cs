using OtpAuth.Application.Challenges;
using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using OtpAuth.Infrastructure.Tests.Devices;
using OtpAuth.Infrastructure.Policy;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class CreateChallengeHandlerTests
{
    private readonly InMemoryChallengeRepository _repository = new();
    private readonly InMemoryDeviceRegistryStore _deviceStore = new();
    private readonly DefaultPolicyEvaluator _policyEvaluator = new();

    [Fact]
    public async Task HandleAsync_CreatesPendingTotpChallenge_WhenTotpIsAvailable()
    {
        var handler = CreateHandler();
        var before = DateTimeOffset.UtcNow;
        var request = CreateValidRequest();

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(ChallengeStatus.Pending, result.Challenge!.Status);
        Assert.Equal(FactorType.Totp, result.Challenge.FactorType);
        Assert.Null(result.Challenge.TargetDeviceId);
        Assert.InRange(result.Challenge.ExpiresAt, before.AddMinutes(4), before.AddMinutes(6));

        var persisted = await _repository.GetByIdAsync(
            result.Challenge.Id,
            request.TenantId,
            request.ApplicationClientId,
            CancellationToken.None);
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task HandleAsync_DeniesChallenge_WhenOnlyPushIsRequested()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest() with
        {
            PreferredFactors = [FactorType.Push],
        };

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateChallengeErrorCode.PolicyDenied, result.ErrorCode);
        Assert.Equal("Requested factor 'Push' is not allowed.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_CreatesPushChallenge_WhenExactlyOneActivePushDeviceExists()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest();
        var device = _deviceStore.SeedActiveDevice(
            request.TenantId,
            request.ApplicationClientId,
            request.ExternalUserId,
            "installation-push");

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(FactorType.Push, result.Challenge!.FactorType);
        Assert.Equal(device.Device.Id, result.Challenge.TargetDeviceId);
        var queuedDelivery = Assert.Single(_repository.GetPushDeliveries());
        Assert.Equal(result.Challenge.Id, queuedDelivery.ChallengeId);
        Assert.Equal(device.Device.Id, queuedDelivery.TargetDeviceId);
    }

    [Fact]
    public async Task HandleAsync_FallsBackToTotp_WhenMultipleActivePushDevicesExist()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest();
        _deviceStore.SeedActiveDevice(
            request.TenantId,
            request.ApplicationClientId,
            request.ExternalUserId,
            "installation-1");
        _deviceStore.SeedActiveDevice(
            request.TenantId,
            request.ApplicationClientId,
            request.ExternalUserId,
            "installation-2");

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(FactorType.Totp, result.Challenge!.FactorType);
        Assert.Null(result.Challenge.TargetDeviceId);
        Assert.Empty(_repository.GetPushDeliveries());
    }

    [Fact]
    public async Task HandleAsync_CreatesPushChallenge_WhenExplicitTargetDeviceIsProvided()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest();
        var firstDevice = _deviceStore.SeedActiveDevice(
            request.TenantId,
            request.ApplicationClientId,
            request.ExternalUserId,
            "installation-1");
        _deviceStore.SeedActiveDevice(
            request.TenantId,
            request.ApplicationClientId,
            request.ExternalUserId,
            "installation-2");
        request = request with
        {
            TargetDeviceId = firstDevice.Device.Id,
        };

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(FactorType.Push, result.Challenge!.FactorType);
        Assert.Equal(firstDevice.Device.Id, result.Challenge.TargetDeviceId);
        var queuedDelivery = Assert.Single(_repository.GetPushDeliveries());
        Assert.Equal(firstDevice.Device.Id, queuedDelivery.TargetDeviceId);
    }

    [Fact]
    public async Task HandleAsync_RejectsExplicitTargetDevice_WhenItIsNotPushCapable()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest();
        var device = _deviceStore.SeedActiveDevice(
            request.TenantId,
            request.ApplicationClientId,
            request.ExternalUserId,
            "installation-1",
            pushToken: null);
        request = request with
        {
            TargetDeviceId = device.Device.Id,
        };

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateChallengeErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal(
            "TargetDeviceId must reference an active push-capable device bound to the requested user.",
            result.ErrorMessage);
        Assert.Empty(_repository.GetPushDeliveries());
    }

    [Fact]
    public async Task HandleAsync_CreatesBackupCodeChallenge_WhenBackupCodeIsRequested()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest() with
        {
            OperationType = OperationType.BackupCodeRecovery,
            PreferredFactors = [FactorType.BackupCode],
        };

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(FactorType.BackupCode, result.Challenge!.FactorType);
    }

    [Fact]
    public async Task HandleAsync_RejectsInsecureCallbackUrl()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest() with
        {
            CallbackUrl = new Uri("http://crm.example.com/hooks/otpauth"),
        };

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateChallengeErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("CallbackUrl must use HTTPS.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("https://localhost/hooks/otpauth")]
    [InlineData("https://127.0.0.1/hooks/otpauth")]
    [InlineData("https://10.0.0.5/hooks/otpauth")]
    public async Task HandleAsync_RejectsLoopbackOrPrivateLiteralCallbackUrl(string callbackUrl)
    {
        var handler = CreateHandler();
        var request = CreateValidRequest() with
        {
            CallbackUrl = new Uri(callbackUrl),
        };

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateChallengeErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("CallbackUrl must not target localhost or private network IP literals.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_RejectsUnsupportedOperationType()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest() with
        {
            OperationType = OperationType.DeviceActivation,
        };

        var result = await handler.HandleAsync(request, CreateClientContext(request), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateChallengeErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("OperationType 'DeviceActivation' is not supported for challenge creation.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_DeniesChallenge_WhenScopeIsMissing()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest();
        var clientContext = CreateClientContext(request, Array.Empty<string>());

        var result = await handler.HandleAsync(request, clientContext, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateChallengeErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Scope 'challenges:write' is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_DeniesChallenge_WhenRequestTenantDoesNotMatchAuthenticatedClient()
    {
        var handler = CreateHandler();
        var request = CreateValidRequest();
        var clientContext = CreateClientContext(request) with
        {
            TenantId = Guid.NewGuid(),
        };

        var result = await handler.HandleAsync(request, clientContext, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateChallengeErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Request tenant is outside the authenticated client scope.", result.ErrorMessage);
    }

    private CreateChallengeHandler CreateHandler()
    {
        return new CreateChallengeHandler(_repository, _deviceStore, _policyEvaluator);
    }

    private static CreateChallengeRequest CreateValidRequest()
    {
        return new CreateChallengeRequest
        {
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-123",
            Username = "ivan.petrov",
            OperationType = OperationType.Login,
            OperationDisplayName = "Sign in to CRM",
            PreferredFactors = [FactorType.Push, FactorType.Totp],
            CorrelationId = "auth-req-2026-04-14-001",
            CallbackUrl = new Uri("https://crm.example.com/webhooks/otpauth"),
        };
    }

    private static IntegrationClientContext CreateClientContext(
        CreateChallengeRequest request,
        IReadOnlyCollection<string>? scopes = null)
    {
        return new IntegrationClientContext
        {
            ClientId = "otpauth-crm",
            TenantId = request.TenantId,
            ApplicationClientId = request.ApplicationClientId,
            Scopes = scopes ?? [IntegrationClientScopes.ChallengesWrite, IntegrationClientScopes.ChallengesRead],
        };
    }
}
