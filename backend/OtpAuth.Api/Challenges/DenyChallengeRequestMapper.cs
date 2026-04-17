using OtpAuth.Application.Challenges;

namespace OtpAuth.Api.Challenges;

public static class DenyChallengeRequestMapper
{
    public static DenyPushChallengeRequest Map(Guid challengeId, DenyChallengeHttpRequest request)
    {
        return new DenyPushChallengeRequest
        {
            ChallengeId = challengeId,
            DeviceId = request.DeviceId,
            Reason = string.IsNullOrWhiteSpace(request.Reason)
                ? null
                : request.Reason.Trim(),
        };
    }
}
