using System.IdentityModel.Tokens.Jwt;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class JwtIntegrationAccessTokenIssuerTests
{
    [Fact]
    public async Task IntrospectAsync_ReturnsActiveForFreshToken()
    {
        var client = CreateClient();
        var issuer = CreateIssuer(new InMemoryRevocationStore());
        var token = await issuer.IssueAsync(
            client,
            [IntegrationClientScopes.ChallengesRead],
            CancellationToken.None);

        var introspection = await issuer.IntrospectAsync(token.AccessToken, CancellationToken.None);

        Assert.True(introspection.IsRecognizedToken);
        Assert.True(introspection.IsActive);
        Assert.Equal(client.ClientId, introspection.ClientId);
        Assert.Equal(client.TenantId, introspection.TenantId);
        Assert.Equal(client.ApplicationClientId, introspection.ApplicationClientId);
        Assert.Equal(IntegrationClientScopes.ChallengesRead, introspection.Scope);
        Assert.False(string.IsNullOrWhiteSpace(introspection.JwtId));
    }

    [Fact]
    public async Task IntrospectAsync_ReturnsInactiveWhenTokenIsRevoked()
    {
        var revocationStore = new InMemoryRevocationStore();
        var client = CreateClient();
        var issuer = CreateIssuer(revocationStore);
        var token = await issuer.IssueAsync(
            client,
            [IntegrationClientScopes.ChallengesRead],
            CancellationToken.None);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
        await revocationStore.RevokeAsync(
            new RevokedIntegrationAccessToken
            {
                JwtId = jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value,
                ClientId = client.ClientId,
                RevokedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                Reason = "test",
            },
            CancellationToken.None);

        var introspection = await issuer.IntrospectAsync(token.AccessToken, CancellationToken.None);

        Assert.True(introspection.IsRecognizedToken);
        Assert.False(introspection.IsActive);
    }

    [Fact]
    public async Task IntrospectAsync_ReturnsUnrecognizedForInvalidToken()
    {
        var issuer = CreateIssuer(new InMemoryRevocationStore());

        var introspection = await issuer.IntrospectAsync("not-a-jwt", CancellationToken.None);

        Assert.False(introspection.IsRecognizedToken);
        Assert.False(introspection.IsActive);
    }

    [Fact]
    public async Task IntrospectAsync_RecognizesTokenSignedWithLegacyKeyAfterRotation()
    {
        var client = CreateClient();
        var oldIssuer = new JwtIntegrationAccessTokenIssuer(
            new BootstrapOAuthOptions
            {
                Issuer = "otpauth-tests",
                Audience = "otpauth-api-tests",
                CurrentSigningKeyId = "key-v1",
                CurrentSigningKey = "integration-tests-signing-key-1234567890",
                AccessTokenLifetimeMinutes = 60,
            },
            new InMemoryRevocationStore());
        var token = await oldIssuer.IssueAsync(
            client,
            [IntegrationClientScopes.ChallengesRead],
            CancellationToken.None);

        var rotatedIssuer = new JwtIntegrationAccessTokenIssuer(
            new BootstrapOAuthOptions
            {
                Issuer = "otpauth-tests",
                Audience = "otpauth-api-tests",
                CurrentSigningKeyId = "key-v2",
                CurrentSigningKey = "integration-tests-signing-key-0987654321",
                AdditionalSigningKeys =
                [
                    new BootstrapOAuthSigningKeyOptions
                    {
                        KeyId = "key-v1",
                        Key = "integration-tests-signing-key-1234567890",
                    },
                ],
                AccessTokenLifetimeMinutes = 60,
            },
            new InMemoryRevocationStore());

        var introspection = await rotatedIssuer.IntrospectAsync(token.AccessToken, CancellationToken.None);

        Assert.True(introspection.IsRecognizedToken);
        Assert.True(introspection.IsActive);
        Assert.Equal(client.ClientId, introspection.ClientId);
    }

    [Fact]
    public async Task IssueAsync_WritesCurrentKeyIdIntoJwtHeader()
    {
        var client = CreateClient();
        var issuer = new JwtIntegrationAccessTokenIssuer(
            new BootstrapOAuthOptions
            {
                Issuer = "otpauth-tests",
                Audience = "otpauth-api-tests",
                CurrentSigningKeyId = "key-v2",
                CurrentSigningKey = "integration-tests-signing-key-0987654321",
                AccessTokenLifetimeMinutes = 60,
            },
            new InMemoryRevocationStore());

        var token = await issuer.IssueAsync(
            client,
            [IntegrationClientScopes.ChallengesRead],
            CancellationToken.None);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);

        Assert.Equal("key-v2", jwt.Header.Kid);
    }

    private static IntegrationClient CreateClient()
    {
        return new IntegrationClient
        {
            ClientId = "crm-client",
            TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"),
            ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"),
            ClientSecretHash = "hash",
            AllowedScopes = [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.ChallengesWrite],
        };
    }

    private static JwtIntegrationAccessTokenIssuer CreateIssuer(IIntegrationAccessTokenRevocationStore revocationStore)
    {
        return new JwtIntegrationAccessTokenIssuer(
            new BootstrapOAuthOptions
            {
                Issuer = "otpauth-tests",
                Audience = "otpauth-api-tests",
                CurrentSigningKey = "integration-tests-signing-key-1234567890",
                CurrentSigningKeyId = "key-v1",
                AccessTokenLifetimeMinutes = 60,
            },
            revocationStore);
    }

    private sealed class InMemoryRevocationStore : IIntegrationAccessTokenRevocationStore
    {
        private readonly HashSet<string> _revokedTokenIds = [];

        public Task<bool> IsRevokedAsync(string jwtId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_revokedTokenIds.Contains(jwtId));
        }

        public Task RevokeAsync(RevokedIntegrationAccessToken token, CancellationToken cancellationToken)
        {
            _revokedTokenIds.Add(token.JwtId);
            return Task.CompletedTask;
        }
    }
}
