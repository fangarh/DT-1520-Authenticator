using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public sealed record ChallengeUpdateSideEffects
{
    public ChallengeCallbackDelivery? CallbackDelivery { get; init; }

    public WebhookEventPublication? WebhookEvent { get; init; }

    public static ChallengeUpdateSideEffects? CreateForTerminalState(Challenge challenge, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var callbackDelivery = ChallengeCallbackDeliveryFactory.CreateFor(challenge, occurredAtUtc);
        var webhookEvent = ChallengeWebhookEventFactory.CreateFor(challenge, occurredAtUtc);

        return callbackDelivery is null && webhookEvent is null
            ? null
            : new ChallengeUpdateSideEffects
            {
                CallbackDelivery = callbackDelivery,
                WebhookEvent = webhookEvent,
            };
    }
}
