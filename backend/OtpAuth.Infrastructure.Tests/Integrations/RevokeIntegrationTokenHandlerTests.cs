using System.IdentityModel.Tokens.Jwt;
using OtpAuth.Application.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class RevokeIntegrationTokenHandlerTests
{
    [Fact]
    public async Task HandleAsync_RevokesRecognizedTokenOwnedByClient()
    {
        var client = CreateClient();
        var revocationStore = new InMemoryRevocationStore();
        var jwtId = Guid.NewGuid().ToString("N");
        var handler = new RevokeIntegrationTokenHandler(
            new StubCredentialsValidator(client),
            new StubIntrospector(new IntegrationAccessTokenIntrospectionResult
            {
                IsRecognizedToken = true,
                IsActive = true,
                ClientId = client.ClientId,
                JwtId = jwtId,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            }),
            revocationStore);

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(jwtId, Assert.Single(revocationStore.Revoked).JwtId);
    }

    [Fact]
    public async Task HandleAsync_DoesNotRevokeForeignToken()
    {
        var client = CreateClient();
        var revocationStore = new InMemoryRevocationStore();
        var handler = new RevokeIntegrationTokenHandler(
            new StubCredentialsValidator(client),
            new StubIntrospector(new IntegrationAccessTokenIntrospectionResult
            {
                IsRecognizedToken = true,
                IsActive = true,
                ClientId = "other-client",
                JwtId = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            }),
            revocationStore);

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(revocationStore.Revoked);
    }

    [Fact]
    public async Task HandleAsync_ReturnsInvalidClient_WhenCredentialsFail()
    {
        var handler = new RevokeIntegrationTokenHandler(
            new StubCredentialsValidator(null),
            new StubIntrospector(IntegrationAccessTokenIntrospectionResult.Unrecognized()),
            new InMemoryRevocationStore());

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RevokeIntegrationTokenErrorCode.InvalidClient, result.ErrorCode);
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

    private static RevokeIntegrationTokenRequest CreateRequest()
    {
        return new RevokeIntegrationTokenRequest
        {
            ClientId = "crm-client",
            ClientSecret = "secret",
            Token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken()),
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

    private sealed class InMemoryRevocationStore : IIntegrationAccessTokenRevocationStore
    {
        public List<RevokedIntegrationAccessToken> Revoked { get; } = [];

        public Task<bool> IsRevokedAsync(string jwtId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Revoked.Any(item => item.JwtId == jwtId));
        }

        public Task RevokeAsync(RevokedIntegrationAccessToken token, CancellationToken cancellationToken)
        {
            Revoked.Add(token);
            return Task.CompletedTask;
        }
    }
}
