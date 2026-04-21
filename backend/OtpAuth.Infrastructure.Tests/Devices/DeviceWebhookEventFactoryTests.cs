using System.Text.Json;
using OtpAuth.Application.Devices;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Devices;

public sealed class DeviceWebhookEventFactoryTests
{
    [Theory]
    [InlineData(DeviceStatus.Active, WebhookEventTypeNames.DeviceActivated)]
    [InlineData(DeviceStatus.Revoked, WebhookEventTypeNames.DeviceRevoked)]
    [InlineData(DeviceStatus.Blocked, WebhookEventTypeNames.DeviceBlocked)]
    public void CreateFor_ReturnsPublication_ForLifecycleWebhookStates(
        DeviceStatus status,
        string expectedEventType)
    {
        var occurredAtUtc = DateTimeOffset.Parse("2026-04-20T15:00:00Z");
        var device = CreateDevice(status);

        var publication = DeviceWebhookEventFactory.CreateFor(device, occurredAtUtc);

        Assert.NotNull(publication);
        Assert.Equal(expectedEventType, publication!.EventType);
        Assert.Equal(WebhookResourceTypeNames.Device, publication.ResourceType);
        Assert.Equal(device.Id, publication.ResourceId);

        using var payload = JsonDocument.Parse(publication.PayloadJson);
        var deviceNode = payload.RootElement.GetProperty("device");
        Assert.Equal(device.Id, deviceNode.GetProperty("id").GetGuid());
        Assert.Equal(device.ExternalUserId, deviceNode.GetProperty("externalUserId").GetString());
        Assert.Equal(device.Status.ToString().ToLowerInvariant(), deviceNode.GetProperty("status").GetString());
    }

    [Fact]
    public void CreateFor_ReturnsNull_ForPendingDevice()
    {
        var device = CreateDevice(DeviceStatus.Pending);

        var publication = DeviceWebhookEventFactory.CreateFor(device, DateTimeOffset.UtcNow);

        Assert.Null(publication);
    }

    private static RegisteredDevice CreateDevice(DeviceStatus status)
    {
        var activatedDevice = RegisteredDevice.Activate(
            Guid.Parse("fb2ad8cc-9ed0-4182-96af-a296b80b0bb2"),
            DeviceApiTestFactory.TenantId,
            DeviceApiTestFactory.ApplicationClientId,
            "user-device-webhook",
            DevicePlatform.Android,
            "installation-device-webhook",
            "Pixel 10 Pro",
            "push-token",
            null,
            DateTimeOffset.Parse("2026-04-20T14:30:00Z"));

        return status switch
        {
            DeviceStatus.Active => activatedDevice,
            DeviceStatus.Revoked => activatedDevice.MarkRevoked(DateTimeOffset.Parse("2026-04-20T14:45:00Z")),
            DeviceStatus.Blocked => activatedDevice.MarkBlocked(DateTimeOffset.Parse("2026-04-20T14:50:00Z")),
            DeviceStatus.Pending => activatedDevice with
            {
                Status = DeviceStatus.Pending,
                ActivatedUtc = null,
                LastSeenUtc = null,
                RevokedUtc = null,
                BlockedUtc = null,
            },
            _ => throw new InvalidOperationException($"Unsupported test status '{status}'."),
        };
    }
}
