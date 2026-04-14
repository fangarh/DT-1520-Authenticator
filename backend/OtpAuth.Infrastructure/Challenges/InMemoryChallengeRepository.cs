using System.Collections.Concurrent;
using OtpAuth.Application.Challenges;
using OtpAuth.Domain.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class InMemoryChallengeRepository : IChallengeRepository
{
    private readonly ConcurrentDictionary<Guid, Challenge> _challenges = new();

    public Task AddAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        _challenges[challenge.Id] = challenge;
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
}
