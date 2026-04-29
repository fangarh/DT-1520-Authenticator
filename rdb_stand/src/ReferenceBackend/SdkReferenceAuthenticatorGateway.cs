using Dt1520.Authenticator.Client;
using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed class SdkReferenceAuthenticatorGateway(
    Dt1520AuthenticatorClient client,
    IOptions<ReferenceBackendOptions> options)
    : IReferenceAuthenticatorGateway
{
    private readonly Dt1520AuthenticatorClient _client = client;
    private readonly ReferenceBackendOptions _options = options.Value;

    public Task<Dt1520AuthenticatorResult<ChallengeResponse>> CreateChallengeAsync(
        ProtectedOperationRecord operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return _client.CreateChallengeAsync(BuildCreateChallengeRequest(
            operation,
            [ChallengeFactorType.Push, ChallengeFactorType.Totp],
            operation.SessionId), cancellationToken);
    }

    public Task<Dt1520AuthenticatorResult<ChallengeResponse>> CreateTotpFallbackChallengeAsync(
        ProtectedOperationRecord operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return _client.CreateChallengeAsync(BuildCreateChallengeRequest(
            operation,
            [ChallengeFactorType.Totp],
            $"{operation.SessionId}:totp-fallback"), cancellationToken);
    }

    public Task<Dt1520AuthenticatorResult<ChallengeResponse>> VerifyTotpAsync(
        Guid challengeId,
        string code,
        CancellationToken cancellationToken)
    {
        return _client.VerifyTotpAsync(
            challengeId,
            new VerifyTotpRequest { Code = code },
            cancellationToken);
    }

    private CreateChallengeRequest BuildCreateChallengeRequest(
        ProtectedOperationRecord operation,
        IReadOnlyCollection<ChallengeFactorType> preferredFactors,
        string idempotencyKey)
    {
        return new CreateChallengeRequest
        {
            TenantId = _options.TenantId,
            ApplicationClientId = _options.ApplicationClientId,
            Subject = new ChallengeSubject
            {
                ExternalUserId = operation.ExternalUserId,
                Username = operation.Username,
            },
            Operation = new ChallengeOperation
            {
                Type = ChallengeOperationType.StepUp,
                DisplayName = operation.DisplayName,
            },
            PreferredFactors = preferredFactors,
            Callback = new ChallengeCallbackRegistration
            {
                Url = _options.CallbackUrl!,
            },
            CorrelationId = operation.SessionId,
            IdempotencyKey = idempotencyKey,
        };
    }
}
