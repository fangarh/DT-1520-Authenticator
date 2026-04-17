using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Infrastructure.Tests.Devices;

internal sealed class StubDeviceAccessTokenIssuer : IDeviceAccessTokenIssuer
{
    public Task<DeviceTokenMaterial> IssueAsync(RegisteredDevice device, Guid tokenFamilyId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var refreshTokenId = Guid.NewGuid();
        var refreshTokenSecret = "replacement-secret";

        return Task.FromResult(new DeviceTokenMaterial
        {
            RefreshTokenId = refreshTokenId,
            RefreshTokenSecret = refreshTokenSecret,
            RefreshTokenExpiresUtc = now.AddDays(30),
            TokenPair = new DeviceTokenPair
            {
                AccessToken = "device-access-token",
                RefreshToken = DeviceRefreshTokenFormat.Create(refreshTokenId, refreshTokenSecret),
                TokenType = "Bearer",
                ExpiresIn = 900,
                Scope = DeviceTokenScope.Challenge,
            },
        });
    }
}
