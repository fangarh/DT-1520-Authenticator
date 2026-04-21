using OtpAuth.Application.Devices;
using OtpAuth.Application.Integrations;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Devices;

public sealed class DeviceLifecycleWebhookPublicationTests
{
    [Fact]
    public async Task ActivateDeviceHandler_EnqueuesTopLevelWebhook_ForMatchingSubscription()
    {
        var store = new InMemoryDeviceRegistryStore();
        store.SeedWebhookSubscription(CreateSubscription(WebhookEventTypeNames.DeviceActivated));
        var activationCode = store.SeedActivationCode(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-device-activate",
            DevicePlatform.Android,
            "device-activation-secret");
        var handler = new ActivateDeviceHandler(
            store,
            new OtpAuth.Infrastructure.Devices.Pbkdf2DeviceRefreshTokenHasher(),
            new StubDeviceAccessTokenIssuer(),
            new RecordingDeviceLifecycleAuditWriter());

        var result = await handler.HandleAsync(
            new ActivateDeviceRequest
            {
                TenantId = DeviceApiTestFactory.TenantId,
                ExternalUserId = "user-device-activate",
                Platform = DevicePlatform.Android,
                ActivationCode = activationCode.PlaintextCode,
                InstallationId = "installation-device-activate",
                DeviceName = "Pixel 10 Pro",
            },
            CreateClientContext(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var delivery = Assert.Single(store.GetWebhookDeliveries());
        Assert.Equal(WebhookEventTypeNames.DeviceActivated, delivery.EventType);
        Assert.Equal(WebhookResourceTypeNames.Device, delivery.ResourceType);
        Assert.Equal("https://crm.example.com/webhooks/devices", delivery.EndpointUrl.ToString());
    }

    [Fact]
    public async Task RevokeDeviceHandler_EnqueuesTopLevelWebhook_WhenStateChanges()
    {
        var store = new InMemoryDeviceRegistryStore();
        store.SeedWebhookSubscription(CreateSubscription(WebhookEventTypeNames.DeviceRevoked));
        var seededDevice = store.SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-device-revoke",
            "installation-device-revoke");
        var handler = new RevokeDeviceHandler(store, new RecordingDeviceLifecycleAuditWriter());

        var result = await handler.HandleAsync(
            seededDevice.Device.Id,
            CreateClientContext(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var delivery = Assert.Single(store.GetWebhookDeliveries());
        Assert.Equal(WebhookEventTypeNames.DeviceRevoked, delivery.EventType);
        Assert.Equal(seededDevice.Device.Id, delivery.ResourceId);
    }

    [Fact]
    public async Task RefreshDeviceTokenHandler_EnqueuesTopLevelWebhook_WhenReuseBlocksDevice()
    {
        var store = new InMemoryDeviceRegistryStore();
        store.SeedWebhookSubscription(CreateSubscription(WebhookEventTypeNames.DeviceBlocked));
        var seededDevice = store.SeedActiveDevice(
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-device-block",
            "installation-device-block");
        var hasher = new OtpAuth.Infrastructure.Devices.Pbkdf2DeviceRefreshTokenHasher();
        var handler = new RefreshDeviceTokenHandler(
            store,
            hasher,
            new StubDeviceAccessTokenIssuer(),
            new RecordingDeviceLifecycleAuditWriter());

        await store.RotateRefreshTokenAsync(
            new DeviceRefreshRotation
            {
                CurrentTokenId = seededDevice.RefreshTokenRecord.TokenId,
                NewToken = new DeviceRefreshTokenRecord
                {
                    TokenId = Guid.NewGuid(),
                    DeviceId = seededDevice.Device.Id,
                    TokenFamilyId = seededDevice.RefreshTokenRecord.TokenFamilyId,
                    TokenHash = hasher.Hash("replacement-secret"),
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

        Assert.False(result.IsSuccess);
        var delivery = Assert.Single(store.GetWebhookDeliveries());
        Assert.Equal(WebhookEventTypeNames.DeviceBlocked, delivery.EventType);
        Assert.Equal(seededDevice.Device.Id, delivery.ResourceId);
    }

    private static IntegrationClientContext CreateClientContext()
    {
        return new IntegrationClientContext
        {
            ClientId = "client-device-webhook",
            TenantId = DeviceApiTestFactory.TenantId,
            ApplicationClientId = DeviceApiTestFactory.ApplicationClientId,
            Scopes = [IntegrationClientScopes.DevicesWrite],
        };
    }

    private static WebhookSubscription CreateSubscription(string eventType)
    {
        return new WebhookSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            TenantId = DeviceApiTestFactory.TenantId,
            ApplicationClientId = DeviceApiTestFactory.ApplicationClientId,
            EndpointUrl = new Uri("https://crm.example.com/webhooks/devices"),
            IsActive = true,
            EventTypes = [eventType],
            CreatedUtc = DateTimeOffset.UtcNow,
        };
    }
}
