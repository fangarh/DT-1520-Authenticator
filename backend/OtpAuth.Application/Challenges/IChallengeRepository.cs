using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public interface IChallengeRepository
{
    Task AddAsync(Challenge challenge, CancellationToken cancellationToken);

    Task<Challenge?> GetByIdAsync(
        Guid challengeId,
        Guid tenantId,
        Guid applicationClientId,
        CancellationToken cancellationToken);

    Task UpdateAsync(Challenge challenge, CancellationToken cancellationToken);
}
