using System.Collections.Concurrent;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class InMemoryChallengeRepository : IChallengeRepository
{
    private readonly ConcurrentDictionary<Guid, Challenge> _challenges = new();
    private readonly ConcurrentDictionary<Guid, PushChallengeDelivery> _pushDeliveries = new();
    private readonly ConcurrentDictionary<Guid, ChallengeCallbackDelivery> _callbackDeliveries = new();
    private readonly ConcurrentDictionary<Guid, WebhookEventDelivery> _webhookDeliveries = new();
    private readonly ConcurrentDictionary<Guid, WebhookSubscription> _subscriptions = new();

    public Task AddAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        return AddAsync(challenge, pushDelivery: null, cancellationToken);
    }

    public Task AddAsync(Challenge challenge, PushChallengeDelivery? pushDelivery, CancellationToken cancellationToken)
    {
        _challenges[challenge.Id] = challenge;
        if (pushDelivery is not null)
        {
            _pushDeliveries[pushDelivery.DeliveryId] = pushDelivery;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Challenge>> ListPendingPushByTargetDeviceAsync(
        Guid targetDeviceId,
        Guid tenantId,
        Guid applicationClientId,
        DateTimeOffset utcNow,
        int maxResults,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Challenge> challenges = _challenges.Values
            .Where(challenge =>
                challenge.TenantId == tenantId &&
                challenge.ApplicationClientId == applicationClientId &&
                challenge.FactorType == Domain.Policy.FactorType.Push &&
                challenge.Status == ChallengeStatus.Pending &&
                challenge.TargetDeviceId == targetDeviceId &&
                challenge.ExpiresAt > utcNow)
            .OrderBy(challenge => challenge.ExpiresAt)
            .ThenBy(challenge => challenge.Id)
            .Take(maxResults)
            .ToArray();

        return Task.FromResult(challenges);
    }

    public Task<Challenge?> GetByIdAsync(
        Guid challengeId,
        Guid tenantId,
        Guid applicationClientId,
        CancellationToken cancellationToken)
    {
        _challenges.TryGetValue(challengeId, out var challenge);
        if (challenge is null ||
            challenge.TenantId != tenantId ||
            challenge.ApplicationClientId != applicationClientId)
        {
            return Task.FromResult<Challenge?>(null);
        }

        return Task.FromResult<Challenge?>(challenge);
    }

    public Task UpdateAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        return UpdateAsync(challenge, sideEffects: null, cancellationToken);
    }

    public Task UpdateAsync(
        Challenge challenge,
        ChallengeUpdateSideEffects? sideEffects,
        CancellationToken cancellationToken)
    {
        _challenges[challenge.Id] = challenge;
        if (sideEffects?.CallbackDelivery is not null)
        {
            _callbackDeliveries[sideEffects.CallbackDelivery.DeliveryId] = sideEffects.CallbackDelivery;
        }

        if (sideEffects?.WebhookEvent is not null)
        {
            foreach (var subscription in _subscriptions.Values.Where(subscription =>
                         subscription.IsActive &&
                         subscription.TenantId == sideEffects.WebhookEvent.TenantId &&
                         subscription.ApplicationClientId == sideEffects.WebhookEvent.ApplicationClientId &&
                         subscription.EventTypes.Contains(sideEffects.WebhookEvent.EventType, StringComparer.Ordinal)))
            {
                var deliveryId = Guid.NewGuid();
                _webhookDeliveries[deliveryId] = new WebhookEventDelivery
                {
                    DeliveryId = deliveryId,
                    SubscriptionId = subscription.SubscriptionId,
                    TenantId = subscription.TenantId,
                    ApplicationClientId = subscription.ApplicationClientId,
                    EndpointUrl = subscription.EndpointUrl,
                    EventId = sideEffects.WebhookEvent.EventId,
                    EventType = sideEffects.WebhookEvent.EventType,
                    OccurredAtUtc = sideEffects.WebhookEvent.OccurredAtUtc,
                    ResourceType = sideEffects.WebhookEvent.ResourceType,
                    ResourceId = sideEffects.WebhookEvent.ResourceId,
                    PayloadJson = sideEffects.WebhookEvent.PayloadJson,
                    Status = WebhookEventDeliveryStatus.Queued,
                    AttemptCount = 0,
                    NextAttemptUtc = sideEffects.WebhookEvent.OccurredAtUtc,
                    CreatedUtc = sideEffects.WebhookEvent.OccurredAtUtc,
                };
            }
        }

        return Task.CompletedTask;
    }

    public IReadOnlyCollection<PushChallengeDelivery> GetPushDeliveries()
    {
        return _pushDeliveries.Values
            .OrderBy(delivery => delivery.CreatedUtc)
            .ToArray();
    }

    public IReadOnlyCollection<ChallengeCallbackDelivery> GetCallbackDeliveries()
    {
        return _callbackDeliveries.Values
            .OrderBy(delivery => delivery.CreatedUtc)
            .ToArray();
    }

    public IReadOnlyCollection<WebhookEventDelivery> GetWebhookDeliveries()
    {
        return _webhookDeliveries.Values
            .OrderBy(delivery => delivery.CreatedUtc)
            .ToArray();
    }

    public void SeedWebhookSubscription(WebhookSubscription subscription)
    {
        _subscriptions[subscription.SubscriptionId] = subscription;
    }
}
