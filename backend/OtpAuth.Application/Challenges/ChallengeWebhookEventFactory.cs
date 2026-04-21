using System.Text.Json;
using OtpAuth.Domain.Challenges;
using OtpAuth.Application.Webhooks;

namespace OtpAuth.Application.Challenges;

public static class ChallengeWebhookEventFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static WebhookEventPublication? CreateFor(Challenge challenge, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var eventType = challenge.Status switch
        {
            ChallengeStatus.Approved => WebhookEventTypeNames.ChallengeApproved,
            ChallengeStatus.Denied => WebhookEventTypeNames.ChallengeDenied,
            ChallengeStatus.Expired => WebhookEventTypeNames.ChallengeExpired,
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
                challenge = new
                {
                    id = challenge.Id,
                    tenantId = challenge.TenantId,
                    applicationClientId = challenge.ApplicationClientId,
                    factorType = challenge.FactorType.ToString().ToLowerInvariant(),
                    status = challenge.Status.ToString().ToLowerInvariant(),
                    expiresAt = challenge.ExpiresAt,
                    targetDeviceId = challenge.TargetDeviceId,
                    approvedAt = challenge.ApprovedUtc,
                    deniedAt = challenge.DeniedUtc,
                    correlationId = challenge.CorrelationId,
                }
            },
            SerializerOptions);

        return new WebhookEventPublication
        {
            EventId = eventId,
            TenantId = challenge.TenantId,
            ApplicationClientId = challenge.ApplicationClientId,
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            ResourceType = WebhookResourceTypeNames.Challenge,
            ResourceId = challenge.Id,
            PayloadJson = payloadJson,
        };
    }
}
