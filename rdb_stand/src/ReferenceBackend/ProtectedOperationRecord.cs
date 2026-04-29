using Dt1520.Authenticator.Client;
using Dt1520.Authenticator.Desktop;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed record ProtectedOperationRecord
{
    public required string SessionId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }

    public required string DisplayName { get; init; }

    public Guid? ChallengeId { get; init; }

    public Guid? TotpChallengeId { get; init; }

    public DesktopApprovalSessionStatus Status { get; init; } = DesktopApprovalSessionStatus.Waiting;

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? DesktopSubmittedAtUtc { get; init; }

    public DateTimeOffset? BackendChallengeRequestedAtUtc { get; init; }

    public DateTimeOffset? ChallengeCreatedAtUtc { get; init; }

    public DateTimeOffset? TotpChallengeRequestedAtUtc { get; init; }

    public DateTimeOffset? TotpChallengeCreatedAtUtc { get; init; }

    public DateTimeOffset? CallbackReceivedAtUtc { get; init; }

    public DateTimeOffset? TotpSubmittedAtUtc { get; init; }

    public DateTimeOffset? TerminalAtUtc { get; init; }

    public DateTimeOffset? CommittedAtUtc { get; init; }

    public string? FailureReason { get; init; }

    public ReferenceApprovalSession ToSession()
    {
        return new ReferenceApprovalSession
        {
            SessionId = SessionId,
            PollingPath = $"/api/reference/operations/{Uri.EscapeDataString(SessionId)}/status",
            Status = Status,
            ExpiresAt = ExpiresAt,
            DisplayMessage = BuildDisplayMessage(),
            FailureReason = FailureReason,
            IsCommitted = CommittedAtUtc is not null,
            Latency = new ReferenceLatencyTimestamps
            {
                DesktopSubmittedAtUtc = DesktopSubmittedAtUtc,
                BackendChallengeRequestedAtUtc = BackendChallengeRequestedAtUtc,
                ChallengeCreatedAtUtc = ChallengeCreatedAtUtc,
                TotpChallengeRequestedAtUtc = TotpChallengeRequestedAtUtc,
                TotpChallengeCreatedAtUtc = TotpChallengeCreatedAtUtc,
                CallbackReceivedAtUtc = CallbackReceivedAtUtc,
                TotpSubmittedAtUtc = TotpSubmittedAtUtc,
                TerminalAtUtc = TerminalAtUtc,
            },
        };
    }

    public bool IsKnownChallenge(Guid challengeId)
    {
        return ChallengeId == challengeId || TotpChallengeId == challengeId;
    }

    public static DesktopApprovalSessionStatus MapChallengeStatus(ChallengeStatus status)
    {
        return status switch
        {
            ChallengeStatus.Pending => DesktopApprovalSessionStatus.Waiting,
            ChallengeStatus.Approved => DesktopApprovalSessionStatus.Approved,
            ChallengeStatus.Denied => DesktopApprovalSessionStatus.Denied,
            ChallengeStatus.Expired => DesktopApprovalSessionStatus.Expired,
            ChallengeStatus.Failed => DesktopApprovalSessionStatus.Failed,
            _ => DesktopApprovalSessionStatus.Failed,
        };
    }

    private string BuildDisplayMessage()
    {
        return Status switch
        {
            DesktopApprovalSessionStatus.Waiting => "Waiting for DT-1520 approval.",
            DesktopApprovalSessionStatus.Approved => "Operation approved.",
            DesktopApprovalSessionStatus.Denied => "Operation denied.",
            DesktopApprovalSessionStatus.Expired => "Approval expired.",
            DesktopApprovalSessionStatus.Cancelled => "Approval cancelled.",
            _ => "Approval failed.",
        };
    }
}
