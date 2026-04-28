using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.ReferenceBackend;

public interface IProtectedOperationStore
{
    Task<ProtectedOperationRecord> CreatePendingAsync(
        StartProtectedOperationRequest request,
        DateTimeOffset utcNow,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task<ProtectedOperationRecord?> GetAsync(string sessionId, CancellationToken cancellationToken);

    Task<ProtectedOperationRecord?> BindChallengeAsync(
        string sessionId,
        ChallengeResponse challenge,
        DateTimeOffset requestedAtUtc,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken);

    Task<ProtectedOperationRecord?> ApplyChallengeStatusAsync(
        string sessionId,
        Guid challengeId,
        ChallengeStatus challengeStatus,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? totpSubmittedAtUtc,
        CancellationToken cancellationToken);

    Task<ProtectedOperationRecord?> MarkFailedAsync(
        string sessionId,
        string failureReason,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);
}
