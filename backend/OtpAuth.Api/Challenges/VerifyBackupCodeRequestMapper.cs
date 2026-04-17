using OtpAuth.Application.Challenges;

namespace OtpAuth.Api.Challenges;

public static class VerifyBackupCodeRequestMapper
{
    public static VerifyBackupCodeRequest Map(Guid challengeId, VerifyBackupCodeHttpRequest httpRequest)
    {
        return new VerifyBackupCodeRequest
        {
            ChallengeId = challengeId,
            Code = httpRequest.Code,
        };
    }
}
