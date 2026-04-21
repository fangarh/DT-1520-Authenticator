using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public static class ChallengeCallbackDeliveryFactory
{
    public static ChallengeCallbackDelivery? CreateFor(Challenge challenge, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (challenge.CallbackUrl is null)
        {
            return null;
        }

        ChallengeCallbackEventType? eventType = challenge.Status switch
        {
            ChallengeStatus.Approved => ChallengeCallbackEventType.Approved,
            ChallengeStatus.Denied => ChallengeCallbackEventType.Denied,
            ChallengeStatus.Expired => ChallengeCallbackEventType.Expired,
            _ => null,
        };

        return eventType.HasValue
            ? ChallengeCallbackDelivery.CreateQueued(challenge, eventType.Value, occurredAtUtc)
            : null;
    }
}
