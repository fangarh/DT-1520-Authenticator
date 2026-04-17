using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Integrations;

namespace OtpAuth.Infrastructure.Devices;

public sealed class JwtDeviceAccessTokenIssuer : IDeviceAccessTokenIssuer
{
    private readonly DeviceTokenOptions _options;
    private readonly BootstrapSigningKeyRing _signingKeyRing;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtDeviceAccessTokenIssuer(
        DeviceTokenOptions options,
        BootstrapOAuthOptions bootstrapOAuthOptions)
    {
        _options = options;
        _signingKeyRing = new BootstrapSigningKeyRing(bootstrapOAuthOptions);
    }

    public Task<DeviceTokenMaterial> IssueAsync(
        RegisteredDevice device,
        Guid tokenFamilyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var expiresAtUtc = now.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var refreshTokenId = Guid.NewGuid();
        var refreshTokenSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var scope = DeviceTokenScope.Challenge;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, device.Id.ToString()),
            new("device_id", device.Id.ToString()),
            new("tenant_id", device.TenantId.ToString()),
            new("application_client_id", device.ApplicationClientId.ToString()),
            new("installation_id", device.InstallationId),
            new("scope", scope),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now.UtcDateTime).ToString(), ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: _signingKeyRing.CurrentSigningCredentials);
        token.Header["kid"] = _signingKeyRing.CurrentSigningKeyId;

        return Task.FromResult(new DeviceTokenMaterial
        {
            RefreshTokenId = refreshTokenId,
            RefreshTokenSecret = refreshTokenSecret,
            RefreshTokenExpiresUtc = now.AddDays(_options.RefreshTokenLifetimeDays),
            TokenPair = new DeviceTokenPair
            {
                AccessToken = _tokenHandler.WriteToken(token),
                RefreshToken = DeviceRefreshTokenFormat.Create(refreshTokenId, refreshTokenSecret),
                TokenType = "Bearer",
                ExpiresIn = (int)(expiresAtUtc - now).TotalSeconds,
                Scope = scope,
            },
        });
    }

    public TokenValidationParameters CreateTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKeyResolver = ResolveSigningKeys,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "device_id",
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
}
