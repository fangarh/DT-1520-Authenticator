using OtpAuth.Application.Challenges;

namespace OtpAuth.Api.Challenges;

public static class ApproveChallengeRequestMapper
{
    public static ApprovePushChallengeRequest Map(Guid challengeId, ApproveChallengeHttpRequest request)
    {
        return new ApprovePushChallengeRequest
        {
            ChallengeId = challengeId,
            DeviceId = request.DeviceId,
            BiometricVerified = request.BiometricVerified,
        };
    }
}
