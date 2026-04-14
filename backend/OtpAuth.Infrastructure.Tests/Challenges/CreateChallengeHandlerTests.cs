using OtpAuth.Application.Challenges;
using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using OtpAuth.Infrastructure.Policy;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class CreateChallengeHandlerTests
{
    private readonly InMemoryChallengeRepository _repository = new();
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
        return new CreateChallengeHandler(_repository, _policyEvaluator);
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
