using System.Collections.Concurrent;
using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed class InMemoryProtectedOperationStore : IProtectedOperationStore
{
    private readonly ConcurrentDictionary<string, ProtectedOperationRecord> _records = new();

    public Task<ProtectedOperationRecord> CreatePendingAsync(
        StartProtectedOperationRequest request,
        DateTimeOffset utcNow,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var record = new ProtectedOperationRecord
        {
            SessionId = Guid.NewGuid().ToString("N"),
            ExternalUserId = request.ExternalUserId.Trim(),
            Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            ExpiresAt = expiresAt,
            DesktopSubmittedAtUtc = utcNow,
        };

        _records[record.SessionId] = record;
        return Task.FromResult(record);
    }

    public Task<ProtectedOperationRecord?> GetAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _records.TryGetValue(sessionId, out var record);
        return Task.FromResult(record);
    }

    public Task<ProtectedOperationRecord?> BindChallengeAsync(
        string sessionId,
        ChallengeResponse challenge,
        DateTimeOffset requestedAtUtc,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Update(sessionId, current => current with
        {
            ChallengeId = challenge.Id,
            Status = ProtectedOperationRecord.MapChallengeStatus(challenge.Status),
            ExpiresAt = challenge.ExpiresAt,
            BackendChallengeRequestedAtUtc = requestedAtUtc,
            ChallengeCreatedAtUtc = createdAtUtc,
        }));
    }

    public Task<ProtectedOperationRecord?> ApplyChallengeStatusAsync(
        string sessionId,
        Guid challengeId,
        ChallengeStatus challengeStatus,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? totpSubmittedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Update(sessionId, current =>
        {
            if (current.ChallengeId != challengeId || current.CommittedAtUtc is not null)
            {
                return current;
            }

            var status = ProtectedOperationRecord.MapChallengeStatus(challengeStatus);
            var terminalAt = current.TerminalAtUtc ?? (current.Status == status ? null : observedAtUtc);
            var committedAt = status == Dt1520.Authenticator.Desktop.DesktopApprovalSessionStatus.Approved
                ? current.CommittedAtUtc ?? observedAtUtc
                : current.CommittedAtUtc;

            return current with
            {
                Status = status,
                CallbackReceivedAtUtc = totpSubmittedAtUtc is null ? observedAtUtc : current.CallbackReceivedAtUtc,
                TotpSubmittedAtUtc = totpSubmittedAtUtc ?? current.TotpSubmittedAtUtc,
                TerminalAtUtc = terminalAt ?? current.TerminalAtUtc,
                CommittedAtUtc = committedAt,
            };
        }));
    }

    public Task<ProtectedOperationRecord?> MarkFailedAsync(
        string sessionId,
        string failureReason,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Update(sessionId, current => current with
        {
            Status = Dt1520.Authenticator.Desktop.DesktopApprovalSessionStatus.Failed,
            FailureReason = failureReason,
            TerminalAtUtc = current.TerminalAtUtc ?? utcNow,
        }));
    }

    private ProtectedOperationRecord? Update(
        string sessionId,
        Func<ProtectedOperationRecord, ProtectedOperationRecord> update)
    {
        while (_records.TryGetValue(sessionId, out var current))
        {
            var next = update(current);
            if (_records.TryUpdate(sessionId, next, current))
            {
                return next;
            }
        }

        return null;
    }
}
