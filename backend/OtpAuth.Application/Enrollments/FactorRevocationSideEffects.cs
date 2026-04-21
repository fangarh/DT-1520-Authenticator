using System.Text.Json;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Enrollments;

public sealed record FactorRevocationSideEffects
{
    public WebhookEventPublication? WebhookEvent { get; init; }

    public static FactorRevocationSideEffects CreateForTotp(
        TotpEnrollmentProvisioningRecord enrollment,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(enrollment);

        var webhookEvent = FactorRevocationWebhookEventFactory.CreateForTotp(enrollment, occurredAtUtc);
        return new FactorRevocationSideEffects
        {
            WebhookEvent = webhookEvent,
        };
    }
}

public static class FactorRevocationWebhookEventFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static WebhookEventPublication CreateForTotp(
        TotpEnrollmentProvisioningRecord enrollment,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(enrollment);

        var eventId = Guid.NewGuid();
        const string factorType = "totp";
        var payloadJson = JsonSerializer.Serialize(
            new
            {
                eventId,
                eventType = WebhookEventTypeNames.FactorRevoked,
                occurredAt = occurredAtUtc,
                factorType,
                subject = new
                {
                    enrollment.ExternalUserId,
                }
            },
            SerializerOptions);

        return new WebhookEventPublication
        {
            EventId = eventId,
            TenantId = enrollment.TenantId,
            ApplicationClientId = enrollment.ApplicationClientId,
            EventType = WebhookEventTypeNames.FactorRevoked,
            OccurredAtUtc = occurredAtUtc,
            ResourceType = WebhookResourceTypeNames.Factor,
            ResourceId = enrollment.EnrollmentId,
            PayloadJson = payloadJson,
        };
    }
}
