using System.Security.Claims;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class IntegrationAccessTokenRuntimeValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ReturnsSuccess_ForActiveClientAndToken()
    {
        var client = CreateClient();
        var validator = new IntegrationAccessTokenRuntimeValidator(
            new InMemoryIntegrationClientStore(client),
            new InMemoryRevocationStore());

        var result = await validator.ValidateAsync(CreatePrincipal(client, "jwt-1"), CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFailure_WhenTokenIsRevoked()
    {
        var client = CreateClient();
        var validator = new IntegrationAccessTokenRuntimeValidator(
            new InMemoryIntegrationClientStore(client),
            new InMemoryRevocationStore("jwt-1"));

        var result = await validator.ValidateAsync(CreatePrincipal(client, "jwt-1"), CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Integration access token has been revoked.", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFailure_WhenClaimsDoNotMatchActiveClient()
    {
        var client = CreateClient();
        var validator = new IntegrationAccessTokenRuntimeValidator(
            new InMemoryIntegrationClientStore(client),
            new InMemoryRevocationStore());
        var mismatchedPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("client_id", client.ClientId),
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim("application_client_id", client.ApplicationClientId.ToString()),
            new Claim("jti", "jwt-1"),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        ], "Bearer"));

        var result = await validator.ValidateAsync(mismatchedPrincipal, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Integration access token claims do not match the active client.", result.ErrorMessage);
    }

    private static IntegrationClient CreateClient()
    {
        return new IntegrationClient
        {
            ClientId = "crm-client",
            TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"),
            ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"),
            ClientSecretHash = "hash",
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
        };
    }

    private static ClaimsPrincipal CreatePrincipal(IntegrationClient client, string jwtId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("client_id", client.ClientId),
            new Claim("tenant_id", client.TenantId.ToString()),
            new Claim("application_client_id", client.ApplicationClientId.ToString()),
            new Claim("jti", jwtId),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        ], "Bearer"));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFailure_WhenTokenWasIssuedBeforeClientAuthStateChanged()
    {
        var client = CreateClient() with
        {
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow,
        };
        var validator = new IntegrationAccessTokenRuntimeValidator(
            new InMemoryIntegrationClientStore(client),
            new InMemoryRevocationStore());
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("client_id", client.ClientId),
            new Claim("tenant_id", client.TenantId.ToString()),
            new Claim("application_client_id", client.ApplicationClientId.ToString()),
            new Claim("jti", "jwt-1"),
            new Claim("iat", DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds().ToString()),
        ], "Bearer"));

        var result = await validator.ValidateAsync(principal, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Integration access token is no longer valid for the current client state.", result.ErrorMessage);
    }

    private sealed class InMemoryIntegrationClientStore : IIntegrationClientStore
    {
        private readonly IntegrationClient? _client;

        public InMemoryIntegrationClientStore(IntegrationClient? client)
        {
            _client = client;
        }

        public Task<IntegrationClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _client is not null && string.Equals(_client.ClientId, clientId, StringComparison.Ordinal)
                    ? _client
                    : null);
        }

        public Task<IReadOnlyCollection<IntegrationClient>> ListActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<IntegrationClient> clients = _client is not null && _client.TenantId == tenantId
                ? [_client]
                : [];
            return Task.FromResult(clients);
        }
    }

    private sealed class InMemoryRevocationStore : IIntegrationAccessTokenRevocationStore
    {
        private readonly HashSet<string> _revokedIds;

        public InMemoryRevocationStore(params string[] revokedIds)
        {
            _revokedIds = revokedIds.ToHashSet(StringComparer.Ordinal);
        }

        public Task<bool> IsRevokedAsync(string jwtId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_revokedIds.Contains(jwtId));
        }

        public Task RevokeAsync(RevokedIntegrationAccessToken token, CancellationToken cancellationToken)
        {
            _revokedIds.Add(token.JwtId);
            return Task.CompletedTask;
        }
    }
}
