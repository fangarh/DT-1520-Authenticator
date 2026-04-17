using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public interface IChallengeRepository
{
    Task AddAsync(Challenge challenge, CancellationToken cancellationToken);

    Task AddAsync(Challenge challenge, PushChallengeDelivery? pushDelivery, CancellationToken cancellationToken);

    Task<Challenge?> GetByIdAsync(
        Guid challengeId,
        Guid tenantId,
        Guid applicationClientId,
        CancellationToken cancellationToken);

    Task UpdateAsync(Challenge challenge, CancellationToken cancellationToken);
}
