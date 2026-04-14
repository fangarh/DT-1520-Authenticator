using OtpAuth.Application.Challenges;

namespace OtpAuth.Api.Challenges;

public static class VerifyTotpRequestMapper
{
    public static VerifyTotpRequest Map(Guid challengeId, VerifyTotpHttpRequest httpRequest)
    {
        return new VerifyTotpRequest
        {
            ChallengeId = challengeId,
            Code = httpRequest.Code,
        };
    }
}
