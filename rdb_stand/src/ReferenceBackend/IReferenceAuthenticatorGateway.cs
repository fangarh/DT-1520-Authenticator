using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.ReferenceBackend;

public interface IReferenceAuthenticatorGateway
{
    Task<Dt1520AuthenticatorResult<ChallengeResponse>> CreateChallengeAsync(
        ProtectedOperationRecord operation,
        CancellationToken cancellationToken);

    Task<Dt1520AuthenticatorResult<ChallengeResponse>> VerifyTotpAsync(
        Guid challengeId,
        string code,
        CancellationToken cancellationToken);
}
