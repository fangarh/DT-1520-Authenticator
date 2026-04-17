using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Devices;

public sealed class RefreshDeviceTokenHandlerTests
{
    [Fact]
    public async Task HandleAsync_BlocksDevice_WhenConsumedRefreshTokenIsReused()
    {
        var store = new InMemoryDeviceRegistryStore();
        var seededDevice = store.SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-reuse",
            "installation-reuse");
        var audit = new RecordingDeviceLifecycleAuditWriter();
        var handler = new RefreshDeviceTokenHandler(
            store,
            new OtpAuth.Infrastructure.Devices.Pbkdf2DeviceRefreshTokenHasher(),
            new StubDeviceAccessTokenIssuer(),
            audit);

        await store.RotateRefreshTokenAsync(
            new DeviceRefreshRotation
            {
                CurrentTokenId = seededDevice.RefreshTokenRecord.TokenId,
                NewToken = new DeviceRefreshTokenRecord
                {
                    TokenId = Guid.NewGuid(),
                    DeviceId = seededDevice.Device.Id,
                    TokenFamilyId = seededDevice.RefreshTokenRecord.TokenFamilyId,
                    TokenHash = new OtpAuth.Infrastructure.Devices.Pbkdf2DeviceRefreshTokenHasher().Hash("new-secret"),
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
                    CreatedUtc = DateTimeOffset.UtcNow,
                },
                RotatedAtUtc = DateTimeOffset.UtcNow,
            },
            seededDevice.Device.Id,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        var result = await handler.HandleAsync(
            new RefreshDeviceTokenRequest
            {
                RefreshToken = seededDevice.PlaintextRefreshToken,
            },
            CancellationToken.None);
        var blockedDevice = await store.GetByIdAsync(seededDevice.Device.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefreshDeviceTokenErrorCode.Conflict, result.ErrorCode);
        Assert.NotNull(blockedDevice);
        Assert.Equal(DeviceStatus.Blocked, blockedDevice!.Status);
        Assert.Contains(audit.Events, entry => entry.StartsWith("refresh_reuse_detected:", StringComparison.Ordinal));
        Assert.Contains(audit.Events, entry => entry.StartsWith("blocked:", StringComparison.Ordinal));
    }
}
