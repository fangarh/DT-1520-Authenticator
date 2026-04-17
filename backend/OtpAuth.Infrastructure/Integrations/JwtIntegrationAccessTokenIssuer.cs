using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class JwtIntegrationAccessTokenIssuer :
    IIntegrationAccessTokenIssuer,
    IIntegrationAccessTokenIntrospector
{
    private readonly BootstrapOAuthOptions _options;
    private readonly IIntegrationClientStore _clientStore;
    private readonly IIntegrationAccessTokenRevocationStore _revocationStore;
    private readonly BootstrapSigningKeyRing _signingKeyRing;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtIntegrationAccessTokenIssuer(
        BootstrapOAuthOptions options,
        IIntegrationClientStore clientStore,
        IIntegrationAccessTokenRevocationStore revocationStore)
    {
        _options = options;
        _clientStore = clientStore;
        _revocationStore = revocationStore;
        _signingKeyRing = new BootstrapSigningKeyRing(options);
    }

    public Task<IssuedAccessToken> IssueAsync(
        IntegrationClient client,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var scopeValue = string.Join(' ', scopes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, client.ClientId),
            new("client_id", client.ClientId),
            new("tenant_id", client.TenantId.ToString()),
            new("application_client_id", client.ApplicationClientId.ToString()),
            new("scope", scopeValue),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now.UtcDateTime).ToString(), ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _signingKeyRing.CurrentSigningCredentials);
        token.Header["kid"] = _signingKeyRing.CurrentSigningKeyId;

        var serializedToken = _tokenHandler.WriteToken(token);
        return Task.FromResult(new IssuedAccessToken
        {
            AccessToken = serializedToken,
            TokenType = "Bearer",
            ExpiresIn = (int)(expiresAt - now).TotalSeconds,
            Scope = scopeValue,
        });
    }

    public async Task<IntegrationAccessTokenIntrospectionResult> IntrospectAsync(
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ClaimsPrincipal principal;
        SecurityToken validatedToken;
        try
        {
            principal = _tokenHandler.ValidateToken(
                token,
                CreateTokenValidationParameters(validateLifetime: false),
                out validatedToken);
        }
        catch
        {
            return IntegrationAccessTokenIntrospectionResult.Unrecognized();
        }

        if (validatedToken is not JwtSecurityToken jwtToken)
        {
            return IntegrationAccessTokenIntrospectionResult.Unrecognized();
        }

        var clientId = principal.FindFirst("client_id")?.Value;
        var tenantId = principal.FindFirst("tenant_id")?.Value;
        var applicationClientId = principal.FindFirst("application_client_id")?.Value;
        var scope = principal.FindFirst("scope")?.Value;
        var jwtId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var issuedAtUtc = TryGetIssuedAtUtc(principal);

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(jwtId) ||
            issuedAtUtc is null ||
            !Guid.TryParse(tenantId, out var parsedTenantId) ||
            !Guid.TryParse(applicationClientId, out var parsedApplicationClientId))
        {
            return IntegrationAccessTokenIntrospectionResult.Unrecognized();
        }

        var expiresAtUtc = new DateTimeOffset(jwtToken.ValidTo, TimeSpan.Zero);
        var revoked = await _revocationStore.IsRevokedAsync(jwtId, cancellationToken);
        var client = await _clientStore.GetByClientIdAsync(clientId, cancellationToken);
        var claimsMatchActiveClient = client is not null &&
            client.TenantId == parsedTenantId &&
            client.ApplicationClientId == parsedApplicationClientId;
        var tokenIssuedAfterLastAuthStateChange = client is not null &&
            issuedAtUtc.Value >= client.LastAuthStateChangedUtc;
        var isActive = expiresAtUtc > DateTimeOffset.UtcNow &&
            !revoked &&
            claimsMatchActiveClient &&
            tokenIssuedAfterLastAuthStateChange;

        return new IntegrationAccessTokenIntrospectionResult
        {
            IsRecognizedToken = true,
            IsActive = isActive,
            ClientId = clientId,
            TenantId = parsedTenantId,
            ApplicationClientId = parsedApplicationClientId,
            Scope = scope,
            JwtId = jwtId,
            ExpiresAtUtc = expiresAtUtc,
        };
    }

    public TokenValidationParameters CreateTokenValidationParameters(bool validateLifetime = true)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = validateLifetime,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKeyResolver = ResolveSigningKeys,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "client_id",
            RoleClaimType = "scope",
        };
    }

    private IEnumerable<SecurityKey> ResolveSigningKeys(
        string token,
        SecurityToken securityToken,
        string kid,
        TokenValidationParameters validationParameters)
    {
        return _signingKeyRing.ResolveValidationKeys(kid);
    }

    private static DateTimeOffset? TryGetIssuedAtUtc(ClaimsPrincipal principal)
    {
        var rawIssuedAt = principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
        if (!long.TryParse(rawIssuedAt, out var secondsSinceEpoch))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(secondsSinceEpoch);
    }
}
