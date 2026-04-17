using System.Collections.Concurrent;
using OtpAuth.Application.Challenges;
using OtpAuth.Domain.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class InMemoryChallengeRepository : IChallengeRepository
{
    private readonly ConcurrentDictionary<Guid, Challenge> _challenges = new();
    private readonly ConcurrentDictionary<Guid, PushChallengeDelivery> _pushDeliveries = new();

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
        _challenges[challenge.Id] = challenge;
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<PushChallengeDelivery> GetPushDeliveries()
    {
        return _pushDeliveries.Values
            .OrderBy(delivery => delivery.CreatedUtc)
            .ToArray();
    }
}
