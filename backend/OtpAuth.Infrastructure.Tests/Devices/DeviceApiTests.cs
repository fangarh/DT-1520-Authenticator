using System.Net;
using System.Net.Http.Json;
using OtpAuth.Api.Devices;
using OtpAuth.Domain.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Devices;

public sealed class DeviceApiTests
{
    [Fact]
    public async Task ActivateDevice_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new DeviceApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/devices/activate", CreateActivationRequest("dac_invalid.secret"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ActivateDevice_ReturnsCreated_WhenActivationCodeIsValid()
    {
        await using var factory = new DeviceApiTestFactory();
        var seededActivation = factory.GetStore().SeedActivationCode(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-activate",
            DevicePlatform.Android,
            "bootstrap-activation-secret");
        var audit = factory.GetAuditWriter();
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync("/api/v1/devices/activate", CreateActivationRequest(seededActivation.PlaintextCode, externalUserId: "user-activate"));
        var body = await response.Content.ReadFromJsonAsync<DeviceActivationHttpResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("active", body!.Device.Status);
        Assert.Equal("android", body.Device.Platform);
        Assert.False(string.IsNullOrWhiteSpace(body.Tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.Tokens.RefreshToken));
        Assert.Equal("Bearer", body.Tokens.TokenType);
        Assert.Contains(audit.Events, entry => entry.StartsWith("activated:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActivateDevice_ReturnsConflict_WhenInstallationAlreadyActive()
    {
        await using var factory = new DeviceApiTestFactory();
        var store = factory.GetStore();
        store.SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-conflict",
            "installation-1234");
        var seededActivation = store.SeedActivationCode(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-conflict",
            DevicePlatform.Android,
            "activation-secret-conflict");
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/devices/activate",
            CreateActivationRequest(seededActivation.PlaintextCode, externalUserId: "user-conflict"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ListDevices_ReturnsActiveDevices_ForExternalUser_AndSupportsPushFilter()
    {
        await using var factory = new DeviceApiTestFactory();
        var store = factory.GetStore();
        var pushDevice = store.SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-routing",
            "installation-push");
        store.SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-routing",
            "installation-totp-only",
            pushToken: null);
        using var client = factory.CreateAuthorizedClient();

        var allDevicesResponse = await client.GetAsync("/api/v1/devices?externalUserId=user-routing");
        var pushOnlyResponse = await client.GetAsync("/api/v1/devices?externalUserId=user-routing&pushCapableOnly=true");
        var allDevices = await allDevicesResponse.Content.ReadFromJsonAsync<DeviceHttpResponse[]>();
        var pushOnlyDevices = await pushOnlyResponse.Content.ReadFromJsonAsync<DeviceHttpResponse[]>();

        Assert.Equal(HttpStatusCode.OK, allDevicesResponse.StatusCode);
        Assert.NotNull(allDevices);
        Assert.Equal(2, allDevices!.Length);
        Assert.Contains(allDevices, device => device.Id == pushDevice.Device.Id && device.IsPushCapable);

        Assert.Equal(HttpStatusCode.OK, pushOnlyResponse.StatusCode);
        Assert.NotNull(pushOnlyDevices);
        var pushOnlyDevice = Assert.Single(pushOnlyDevices!);
        Assert.Equal(pushDevice.Device.Id, pushOnlyDevice.Id);
        Assert.True(pushOnlyDevice.IsPushCapable);
    }

    [Fact]
    public async Task RefreshDeviceToken_RotatesTokenPair_AndBlocksOnReuse()
    {
        await using var factory = new DeviceApiTestFactory();
        var seededActivation = factory.GetStore().SeedActivationCode(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-refresh",
            DevicePlatform.Android,
            "bootstrap-activation-secret");
        var audit = factory.GetAuditWriter();
        using var client = factory.CreateAuthorizedClient();

        var activationResponse = await client.PostAsJsonAsync(
            "/api/v1/devices/activate",
            CreateActivationRequest(seededActivation.PlaintextCode, externalUserId: "user-refresh"));
        var activationBody = await activationResponse.Content.ReadFromJsonAsync<DeviceActivationHttpResponse>();
        var firstRefreshToken = activationBody!.Tokens.RefreshToken;

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/device-tokens/refresh",
            new RefreshDeviceTokenHttpRequest
            {
                RefreshToken = firstRefreshToken,
            });
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<DeviceTokenHttpResponse>();

        var replayResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/device-tokens/refresh",
            new RefreshDeviceTokenHttpRequest
            {
                RefreshToken = firstRefreshToken,
            });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(refreshBody);
        Assert.False(string.IsNullOrWhiteSpace(refreshBody!.RefreshToken));
        Assert.NotEqual(firstRefreshToken, refreshBody.RefreshToken);
        Assert.Equal(HttpStatusCode.Conflict, replayResponse.StatusCode);
        Assert.Contains(audit.Events, entry => entry.StartsWith("token_refreshed:", StringComparison.Ordinal));
        Assert.Contains(audit.Events, entry => entry.StartsWith("refresh_reuse_detected:", StringComparison.Ordinal));
        Assert.Contains(audit.Events, entry => entry.StartsWith("blocked:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RevokeDevice_ReturnsRevokedDevice_AndPreventsFutureRefresh()
    {
        await using var factory = new DeviceApiTestFactory();
        var seededDevice = factory.GetStore().SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-revoke",
            "installation-revoke");
        var audit = factory.GetAuditWriter();
        using var client = factory.CreateAuthorizedClient();

        var revokeResponse = await client.PostAsync($"/api/v1/devices/{seededDevice.Device.Id}/revoke", content: null);
        var revokeBody = await revokeResponse.Content.ReadFromJsonAsync<DeviceHttpResponse>();
        var refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/device-tokens/refresh",
            new RefreshDeviceTokenHttpRequest
            {
                RefreshToken = seededDevice.PlaintextRefreshToken,
            });

        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.NotNull(revokeBody);
        Assert.Equal("revoked", revokeBody!.Status);
        Assert.Equal(HttpStatusCode.Conflict, refreshResponse.StatusCode);
        Assert.Contains(audit.Events, entry => entry.StartsWith("revoked:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RevokeDevice_ReturnsForbidden_WhenScopeIsMissing()
    {
        await using var factory = new DeviceApiTestFactory();
        var seededDevice = factory.GetStore().SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-forbidden",
            "installation-forbidden");
        using var client = factory.CreateAuthorizedClient(DeviceApiTestFactory.MissingScopeScenario);

        var response = await client.PostAsync($"/api/v1/devices/{seededDevice.Device.Id}/revoke", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static ActivateDeviceHttpRequest CreateActivationRequest(string activationCode, string externalUserId = "user-activate")
    {
        return new ActivateDeviceHttpRequest
        {
            TenantId = DeviceApiTestFactory.TenantId,
            ExternalUserId = externalUserId,
            Platform = "android",
            ActivationCode = activationCode,
            InstallationId = "installation-1234",
            DeviceName = "Pixel 10 Pro",
        };
    }
}
