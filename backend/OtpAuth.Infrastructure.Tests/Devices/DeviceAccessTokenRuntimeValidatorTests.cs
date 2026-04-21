using System.Security.Claims;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Devices;

public sealed class DeviceAccessTokenRuntimeValidatorTests
{
    [Fact]
    public async Task ValidateAsync_Fails_WhenTokenIssuedBeforeLastAuthStateChange()
    {
        var store = new InMemoryDeviceRegistryStore();
        var seededDevice = store.SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-stale-token",
            "installation-stale");
        await store.RevokeDeviceAsync(
            seededDevice.Device.MarkBlocked(DateTimeOffset.UtcNow),
            DateTimeOffset.UtcNow,
            sideEffects: null,
            CancellationToken.None);

        var validator = new DeviceAccessTokenRuntimeValidator(store);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("device_id", seededDevice.Device.Id.ToString()),
            new Claim("tenant_id", DeviceApiTestFactory.TenantId.ToString()),
            new Claim("application_client_id", DeviceApiTestFactory.ApplicationClientId.ToString()),
            new Claim("iat", DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds().ToString()),
        ], "Test"));

        var result = await validator.ValidateAsync(principal, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Device is not active.", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenClaimsDoNotMatchDevice()
    {
        var store = new InMemoryDeviceRegistryStore();
        var seededDevice = store.SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-claims",
            "installation-claims");
        var validator = new DeviceAccessTokenRuntimeValidator(store);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("device_id", seededDevice.Device.Id.ToString()),
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim("application_client_id", DeviceApiTestFactory.ApplicationClientId.ToString()),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        ], "Test"));

        var result = await validator.ValidateAsync(principal, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Device access token claims do not match the active device.", result.ErrorMessage);
    }
}
