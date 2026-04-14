using System.IdentityModel.Tokens.Jwt;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class IssueIntegrationTokenHandlerTests
{
    [Fact]
    public async Task HandleAsync_IssuesToken_WhenClientCredentialsAndScopesAreValid()
    {
        var hasher = new Pbkdf2ClientSecretHasher();
        var client = new IntegrationClient
        {
            ClientId = "crm-client",
            TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"),
            ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"),
            ClientSecretHash = hasher.Hash("super-secret"),
            AllowedScopes = [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.ChallengesWrite],
        };
        var handler = new IssueIntegrationTokenHandler(
            new IntegrationClientCredentialsValidator(new InMemoryIntegrationClientStore(client), hasher),
            new JwtIntegrationAccessTokenIssuer(new BootstrapOAuthOptions
            {
                Issuer = "otpauth-tests",
                Audience = "otpauth-api-tests",
                SigningKey = "integration-tests-signing-key-1234567890",
                AccessTokenLifetimeMinutes = 60,
            }, new InMemoryRevocationStore()));

        var result = await handler.HandleAsync(
            new IssueIntegrationTokenRequest
            {
                GrantType = "client_credentials",
                ClientId = "crm-client",
                ClientSecret = "super-secret",
                Scope = $"{IntegrationClientScopes.ChallengesRead} {IntegrationClientScopes.ChallengesWrite}",
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Token);
        Assert.Equal("Bearer", result.Token!.TokenType);
        Assert.Equal($"{IntegrationClientScopes.ChallengesRead} {IntegrationClientScopes.ChallengesWrite}", result.Token.Scope);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token.AccessToken);
        Assert.Equal("crm-client", jwt.Claims.Single(claim => claim.Type == "client_id").Value);
        Assert.Equal(client.TenantId.ToString(), jwt.Claims.Single(claim => claim.Type == "tenant_id").Value);
        Assert.Equal(client.ApplicationClientId.ToString(), jwt.Claims.Single(claim => claim.Type == "application_client_id").Value);
    }

    [Fact]
    public async Task HandleAsync_IssuesAllAllowedScopes_WhenScopeIsOmitted()
    {
        var hasher = new Pbkdf2ClientSecretHasher();
        var client = new IntegrationClient
        {
            ClientId = "crm-client",
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ClientSecretHash = hasher.Hash("super-secret"),
            AllowedScopes = [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.ChallengesWrite],
        };
        var handler = new IssueIntegrationTokenHandler(
            new IntegrationClientCredentialsValidator(new InMemoryIntegrationClientStore(client), hasher),
            new FixedAccessTokenIssuer());

        var result = await handler.HandleAsync(
            new IssueIntegrationTokenRequest
            {
                GrantType = "client_credentials",
                ClientId = "crm-client",
                ClientSecret = "super-secret",
                Scope = null,
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Token);
        Assert.Equal($"{IntegrationClientScopes.ChallengesRead} {IntegrationClientScopes.ChallengesWrite}", result.Token!.Scope);
    }

    [Fact]
    public async Task HandleAsync_ReturnsInvalidClient_WhenSecretIsWrong()
    {
        var hasher = new Pbkdf2ClientSecretHasher();
        var client = new IntegrationClient
        {
            ClientId = "crm-client",
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ClientSecretHash = hasher.Hash("super-secret"),
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
        };
        var handler = new IssueIntegrationTokenHandler(
            new IntegrationClientCredentialsValidator(new InMemoryIntegrationClientStore(client), hasher),
            new FixedAccessTokenIssuer());

        var result = await handler.HandleAsync(
            new IssueIntegrationTokenRequest
            {
                GrantType = "client_credentials",
                ClientId = "crm-client",
                ClientSecret = "wrong-secret",
                Scope = IntegrationClientScopes.ChallengesRead,
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IssueIntegrationTokenErrorCode.InvalidClient, result.ErrorCode);
        Assert.Equal("Client authentication failed.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsInvalidScope_WhenClientRequestsForbiddenScope()
    {
        var hasher = new Pbkdf2ClientSecretHasher();
        var client = new IntegrationClient
        {
            ClientId = "crm-client",
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ClientSecretHash = hasher.Hash("super-secret"),
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
        };
        var handler = new IssueIntegrationTokenHandler(
            new IntegrationClientCredentialsValidator(new InMemoryIntegrationClientStore(client), hasher),
            new FixedAccessTokenIssuer());

        var result = await handler.HandleAsync(
            new IssueIntegrationTokenRequest
            {
                GrantType = "client_credentials",
                ClientId = "crm-client",
                ClientSecret = "super-secret",
                Scope = IntegrationClientScopes.ChallengesWrite,
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IssueIntegrationTokenErrorCode.InvalidScope, result.ErrorCode);
        Assert.Equal("Requested scope is not allowed for the client.", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailure_WhenGrantTypeIsUnsupported()
    {
        var handler = new IssueIntegrationTokenHandler(
            new IntegrationClientCredentialsValidator(
                new InMemoryIntegrationClientStore(),
                new Pbkdf2ClientSecretHasher()),
            new FixedAccessTokenIssuer());

        var result = await handler.HandleAsync(
            new IssueIntegrationTokenRequest
            {
                GrantType = "password",
                ClientId = "crm-client",
                ClientSecret = "secret",
                Scope = IntegrationClientScopes.ChallengesRead,
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IssueIntegrationTokenErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("Grant type 'client_credentials' is required.", result.ErrorMessage);
    }

    private sealed class InMemoryIntegrationClientStore : IIntegrationClientStore
    {
        private readonly IReadOnlyDictionary<string, IntegrationClient> _clients;

        public InMemoryIntegrationClientStore(params IntegrationClient[] clients)
        {
            _clients = clients.ToDictionary(client => client.ClientId, StringComparer.Ordinal);
        }

        public Task<IntegrationClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _clients.TryGetValue(clientId, out var client);
            return Task.FromResult(client);
        }
    }

    private sealed class FixedAccessTokenIssuer : IIntegrationAccessTokenIssuer
    {
        public Task<IssuedAccessToken> IssueAsync(
            IntegrationClient client,
            IReadOnlyCollection<string> scopes,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new IssuedAccessToken
            {
                AccessToken = "fixed-token",
                TokenType = "Bearer",
                ExpiresIn = 3600,
                Scope = string.Join(' ', scopes),
            });
        }
    }

    private sealed class InMemoryRevocationStore : IIntegrationAccessTokenRevocationStore
    {
        public Task<bool> IsRevokedAsync(string jwtId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task RevokeAsync(RevokedIntegrationAccessToken token, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
