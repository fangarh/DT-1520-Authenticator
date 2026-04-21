using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public sealed record DeviceLifecycleSideEffects
{
    public WebhookEventPublication? WebhookEvent { get; init; }

    public static DeviceLifecycleSideEffects? CreateFor(RegisteredDevice device, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(device);

        var webhookEvent = DeviceWebhookEventFactory.CreateFor(device, occurredAtUtc);
        return webhookEvent is null
            ? null
            : new DeviceLifecycleSideEffects
            {
                WebhookEvent = webhookEvent,
            };
    }
}
