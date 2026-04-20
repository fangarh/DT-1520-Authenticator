using OtpAuth.Application.Devices;

namespace OtpAuth.Application.Challenges;

public sealed class ListPendingPushChallengesForDeviceHandler
{
    private const int MaxResults = 20;
    private readonly IChallengeRepository _challengeRepository;

    public ListPendingPushChallengesForDeviceHandler(IChallengeRepository challengeRepository)
    {
        _challengeRepository = challengeRepository;
    }

    public async Task<ListPendingPushChallengesForDeviceResult> HandleAsync(
        DeviceClientContext deviceContext,
        CancellationToken cancellationToken)
    {
        if (!deviceContext.HasScope(DeviceTokenScope.Challenge))
        {
            return ListPendingPushChallengesForDeviceResult.Failure(
                ListPendingPushChallengesForDeviceErrorCode.AccessDenied,
                $"Scope '{DeviceTokenScope.Challenge}' is required.");
        }

        var challenges = await _challengeRepository.ListPendingPushByTargetDeviceAsync(
            deviceContext.DeviceId,
            deviceContext.TenantId,
            deviceContext.ApplicationClientId,
            DateTimeOffset.UtcNow,
            MaxResults,
            cancellationToken);

        return ListPendingPushChallengesForDeviceResult.Success(challenges);
    }
}
