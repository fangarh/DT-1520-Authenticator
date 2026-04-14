using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class JwtIntegrationAccessTokenIssuer :
    IIntegrationAccessTokenIssuer,
    IIntegrationAccessTokenIntrospector
{
    private readonly BootstrapOAuthOptions _options;
    private readonly IIntegrationAccessTokenRevocationStore _revocationStore;
    private readonly string _currentSigningKeyId;
    private readonly SigningCredentials _currentSigningCredentials;
    private readonly IReadOnlyDictionary<string, SecurityKey> _signingKeysById;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtIntegrationAccessTokenIssuer(
        BootstrapOAuthOptions options,
        IIntegrationAccessTokenRevocationStore revocationStore)
    {
        _options = options;
        _revocationStore = revocationStore;

        var currentSigningKeyId = string.IsNullOrWhiteSpace(options.CurrentSigningKeyId)
            ? "bootstrap-current"
            : options.CurrentSigningKeyId.Trim();
        var currentSigningKeyMaterial = string.IsNullOrWhiteSpace(options.CurrentSigningKey)
            ? options.SigningKey
            : options.CurrentSigningKey;
        var resolvedCurrentSigningKeyMaterial = string.IsNullOrWhiteSpace(currentSigningKeyMaterial)
            ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            : currentSigningKeyMaterial;

        var signingKeys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal)
        {
            [currentSigningKeyId] = CreateSigningKey(resolvedCurrentSigningKeyMaterial, currentSigningKeyId),
        };

        foreach (var additionalKey in options.AdditionalSigningKeys)
        {
            if (string.IsNullOrWhiteSpace(additionalKey.KeyId))
            {
                throw new InvalidOperationException("BootstrapOAuth additional signing key id must be configured.");
            }

            if (signingKeys.ContainsKey(additionalKey.KeyId))
            {
                throw new InvalidOperationException(
                    $"BootstrapOAuth signing key id '{additionalKey.KeyId}' is configured more than once.");
            }

            signingKeys[additionalKey.KeyId] = CreateSigningKey(additionalKey.Key, additionalKey.KeyId);
        }

        _currentSigningKeyId = currentSigningKeyId;
        _signingKeysById = signingKeys;
        _currentSigningCredentials = new SigningCredentials(
            _signingKeysById[currentSigningKeyId],
            SecurityAlgorithms.HmacSha256);
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
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _currentSigningCredentials);
        token.Header["kid"] = _currentSigningKeyId;

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

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(jwtId) ||
            !Guid.TryParse(tenantId, out var parsedTenantId) ||
            !Guid.TryParse(applicationClientId, out var parsedApplicationClientId))
        {
            return IntegrationAccessTokenIntrospectionResult.Unrecognized();
        }

        var expiresAtUtc = new DateTimeOffset(jwtToken.ValidTo, TimeSpan.Zero);
        var revoked = await _revocationStore.IsRevokedAsync(jwtId, cancellationToken);
        var isActive = expiresAtUtc > DateTimeOffset.UtcNow && !revoked;

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
        if (!string.IsNullOrWhiteSpace(kid) && _signingKeysById.TryGetValue(kid, out var keyedSigningKey))
        {
            return [keyedSigningKey];
        }

        return _signingKeysById.Values;
    }

    private static SecurityKey CreateSigningKey(string signingKeyMaterial, string keyId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyMaterial))
        {
            KeyId = keyId,
        };

        return key;
    }
}
