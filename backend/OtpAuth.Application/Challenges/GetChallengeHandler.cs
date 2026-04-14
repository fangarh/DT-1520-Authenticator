using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Challenges;

public sealed class GetChallengeHandler
{
    private readonly IChallengeRepository _challengeRepository;

    public GetChallengeHandler(IChallengeRepository challengeRepository)
    {
        _challengeRepository = challengeRepository;
    }

    public async Task<GetChallengeResult> HandleAsync(
        Guid challengeId,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        if (challengeId == Guid.Empty)
        {
            return GetChallengeResult.Failure(
                GetChallengeErrorCode.ValidationFailed,
                "ChallengeId is required.");
        }

        if (!clientContext.HasScope(IntegrationClientScopes.ChallengesRead))
        {
            return GetChallengeResult.Failure(
                GetChallengeErrorCode.AccessDenied,
                $"Scope '{IntegrationClientScopes.ChallengesRead}' is required.");
        }

        var challenge = await _challengeRepository.GetByIdAsync(
            challengeId,
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            cancellationToken);
        if (challenge is null)
        {
            return GetChallengeResult.Failure(
                GetChallengeErrorCode.NotFound,
                $"Challenge '{challengeId}' was not found.");
        }

        return GetChallengeResult.Success(challenge);
    }
}
