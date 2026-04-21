using System.Text.Json;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public static class DeviceWebhookEventFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static WebhookEventPublication? CreateFor(RegisteredDevice device, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(device);

        var eventType = device.Status switch
        {
            DeviceStatus.Active when device.ActivatedUtc.HasValue => WebhookEventTypeNames.DeviceActivated,
            DeviceStatus.Revoked when device.RevokedUtc.HasValue => WebhookEventTypeNames.DeviceRevoked,
            DeviceStatus.Blocked when device.BlockedUtc.HasValue => WebhookEventTypeNames.DeviceBlocked,
            _ => null,
        };
        if (eventType is null)
        {
            return null;
        }

        var eventId = Guid.NewGuid();
        var payloadJson = JsonSerializer.Serialize(
            new
            {
                eventId,
                eventType,
                occurredAt = occurredAtUtc,
                device = new
                {
                    id = device.Id,
                    tenantId = device.TenantId,
                    applicationClientId = device.ApplicationClientId,
                    externalUserId = device.ExternalUserId,
                    platform = device.Platform.ToString().ToLowerInvariant(),
                    status = device.Status.ToString().ToLowerInvariant(),
                    deviceName = device.DeviceName,
                    isPushCapable = !string.IsNullOrWhiteSpace(device.PushToken),
                    attestationStatus = device.AttestationStatus.ToString().ToLowerInvariant(),
                    activatedAt = device.ActivatedUtc,
                    lastSeenAt = device.LastSeenUtc,
                    revokedAt = device.RevokedUtc,
                    blockedAt = device.BlockedUtc,
                }
            },
            SerializerOptions);

        return new WebhookEventPublication
        {
            EventId = eventId,
            TenantId = device.TenantId,
            ApplicationClientId = device.ApplicationClientId,
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            ResourceType = WebhookResourceTypeNames.Device,
            ResourceId = device.Id,
            PayloadJson = payloadJson,
        };
    }
}
