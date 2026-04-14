using OtpAuth.Application.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class IntrospectIntegrationTokenHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsActiveFalse_ForForeignToken()
    {
        var client = CreateClient();
        var handler = new IntrospectIntegrationTokenHandler(
            new StubCredentialsValidator(client),
            new StubIntrospector(new IntegrationAccessTokenIntrospectionResult
            {
                IsRecognizedToken = true,
                IsActive = true,
                ClientId = "other-client",
                JwtId = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            }));

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Introspection);
        Assert.False(result.Introspection!.IsActive);
        Assert.False(result.Introspection.IsRecognizedToken);
    }

    [Fact]
    public async Task HandleAsync_ReturnsIntrospection_ForOwnedToken()
    {
        var client = CreateClient();
        var handler = new IntrospectIntegrationTokenHandler(
            new StubCredentialsValidator(client),
            new StubIntrospector(new IntegrationAccessTokenIntrospectionResult
            {
                IsRecognizedToken = true,
                IsActive = true,
                ClientId = client.ClientId,
                TenantId = client.TenantId,
                ApplicationClientId = client.ApplicationClientId,
                Scope = IntegrationClientScopes.ChallengesRead,
                JwtId = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            }));

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Introspection);
        Assert.True(result.Introspection!.IsActive);
        Assert.Equal(client.ClientId, result.Introspection.ClientId);
    }

    [Fact]
    public async Task HandleAsync_ReturnsInvalidClient_WhenCredentialsFail()
    {
        var handler = new IntrospectIntegrationTokenHandler(
            new StubCredentialsValidator(null),
            new StubIntrospector(IntegrationAccessTokenIntrospectionResult.Unrecognized()));

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IntrospectIntegrationTokenErrorCode.InvalidClient, result.ErrorCode);
    }

    private static IntegrationClient CreateClient()
    {
        return new IntegrationClient
        {
            ClientId = "crm-client",
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ClientSecretHash = "hash",
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
        };
    }

    private static IntrospectIntegrationTokenRequest CreateRequest()
    {
        return new IntrospectIntegrationTokenRequest
        {
            ClientId = "crm-client",
            ClientSecret = "secret",
            Token = "token",
            TokenTypeHint = "access_token",
        };
    }

    private sealed class StubCredentialsValidator : IIntegrationClientCredentialsValidator
    {
        private readonly IntegrationClient? _client;

        public StubCredentialsValidator(IntegrationClient? client)
        {
            _client = client;
        }

        public Task<IntegrationClient?> ValidateAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
        {
            return Task.FromResult(_client);
        }
    }

    private sealed class StubIntrospector : IIntegrationAccessTokenIntrospector
    {
        private readonly IntegrationAccessTokenIntrospectionResult _result;

        public StubIntrospector(IntegrationAccessTokenIntrospectionResult result)
        {
            _result = result;
        }

        public Task<IntegrationAccessTokenIntrospectionResult> IntrospectAsync(string token, CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }
}
