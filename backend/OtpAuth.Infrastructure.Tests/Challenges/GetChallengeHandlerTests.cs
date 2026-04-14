using OtpAuth.Application.Challenges;
using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class GetChallengeHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsChallenge_WhenItExists()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);
        var clientContext = CreateClientContext(challenge);

        var handler = new GetChallengeHandler(repository);
        var result = await handler.HandleAsync(challenge.Id, clientContext, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Challenge);
        Assert.Equal(challenge.Id, result.Challenge!.Id);
        Assert.Equal(FactorType.Totp, result.Challenge.FactorType);
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailure_WhenIdentifierIsEmpty()
    {
        var handler = new GetChallengeHandler(new InMemoryChallengeRepository());
        var clientContext = CreateClientContext(CreateChallenge());

        var result = await handler.HandleAsync(Guid.Empty, clientContext, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GetChallengeErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("ChallengeId is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenChallengeDoesNotExist()
    {
        var handler = new GetChallengeHandler(new InMemoryChallengeRepository());
        var challengeId = Guid.NewGuid();
        var clientContext = CreateClientContext(CreateChallenge());

        var result = await handler.HandleAsync(challengeId, clientContext, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GetChallengeErrorCode.NotFound, result.ErrorCode);
        Assert.Equal($"Challenge '{challengeId}' was not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenScopeIsMissing()
    {
        var challenge = CreateChallenge();
        var handler = new GetChallengeHandler(new InMemoryChallengeRepository());
        var clientContext = CreateClientContext(challenge, Array.Empty<string>());

        var result = await handler.HandleAsync(challenge.Id, clientContext, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GetChallengeErrorCode.AccessDenied, result.ErrorCode);
        Assert.Equal("Scope 'challenges:read' is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenChallengeBelongsToDifferentApplicationClient()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        await repository.AddAsync(challenge, CancellationToken.None);

        var handler = new GetChallengeHandler(repository);
        var clientContext = CreateClientContext(challenge) with
        {
            ApplicationClientId = Guid.NewGuid(),
        };

        var result = await handler.HandleAsync(challenge.Id, clientContext, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GetChallengeErrorCode.NotFound, result.ErrorCode);
        Assert.Equal($"Challenge '{challenge.Id}' was not found.", result.ErrorMessage);
    }

    private static Challenge CreateChallenge()
    {
        return new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-456",
            Username = "anna.ivanova",
            OperationType = OperationType.Login,
            OperationDisplayName = "Sign in to Portal",
            FactorType = FactorType.Totp,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CorrelationId = "auth-req-2026-04-14-002",
            CallbackUrl = new Uri("https://portal.example.com/webhooks/challenges"),
        };
    }

    private static IntegrationClientContext CreateClientContext(
        Challenge challenge,
        IReadOnlyCollection<string>? scopes = null)
    {
        return new IntegrationClientContext
        {
            ClientId = "otpauth-crm",
            TenantId = challenge.TenantId,
            ApplicationClientId = challenge.ApplicationClientId,
            Scopes = scopes ?? [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.ChallengesWrite],
        };
    }
}
