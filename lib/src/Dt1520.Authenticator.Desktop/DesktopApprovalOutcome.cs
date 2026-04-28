namespace Dt1520.Authenticator.Desktop;

/// <summary>
/// Desktop approval polling result.
/// </summary>
public sealed record DesktopApprovalOutcome
{
    private DesktopApprovalOutcome(
        DesktopApprovalOutcomeKind kind,
        DesktopApprovalSession session,
        bool isTerminal,
        int? backendStatusCode,
        string? errorMessage)
    {
        Kind = kind;
        Session = session;
        IsTerminal = isTerminal;
        BackendStatusCode = backendStatusCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Normalized outcome kind for UI and workflow code.
    /// </summary>
    public DesktopApprovalOutcomeKind Kind { get; }

    /// <summary>
    /// Latest sanitized approval session state.
    /// </summary>
    public DesktopApprovalSession Session { get; }

    /// <summary>
    /// Indicates whether the desktop UI should stop waiting.
    /// </summary>
    public bool IsTerminal { get; }

    /// <summary>
    /// HTTP status code returned by the integrator backend when polling failed.
    /// </summary>
    public int? BackendStatusCode { get; }

    /// <summary>
    /// Safe local failure message. Raw backend response bodies are not exposed here.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Indicates whether the protected operation may proceed.
    /// </summary>
    public bool IsApproved => Kind == DesktopApprovalOutcomeKind.Approved;

    /// <summary>
    /// Creates an outcome from a backend session state.
    /// </summary>
    public static DesktopApprovalOutcome FromSession(DesktopApprovalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var kind = session.Status switch
        {
            DesktopApprovalSessionStatus.Waiting => DesktopApprovalOutcomeKind.Waiting,
            DesktopApprovalSessionStatus.Approved => DesktopApprovalOutcomeKind.Approved,
            DesktopApprovalSessionStatus.Denied => DesktopApprovalOutcomeKind.Denied,
            DesktopApprovalSessionStatus.Expired => DesktopApprovalOutcomeKind.Expired,
            DesktopApprovalSessionStatus.Failed => DesktopApprovalOutcomeKind.Failed,
            DesktopApprovalSessionStatus.Cancelled => DesktopApprovalOutcomeKind.Cancelled,
            _ => DesktopApprovalOutcomeKind.Failed,
        };

        return new DesktopApprovalOutcome(
            kind,
            session,
            session.IsTerminal,
            backendStatusCode: null,
            errorMessage: null);
    }

    internal static DesktopApprovalOutcome Failed(
        DesktopApprovalSession session,
        string errorMessage,
        int? backendStatusCode = null)
    {
        return new DesktopApprovalOutcome(
            DesktopApprovalOutcomeKind.Failed,
            session with { Status = DesktopApprovalSessionStatus.Failed, FailureReason = "polling_failed" },
            isTerminal: true,
            backendStatusCode,
            errorMessage);
    }

    internal static DesktopApprovalOutcome Cancelled(DesktopApprovalSession session)
    {
        return new DesktopApprovalOutcome(
            DesktopApprovalOutcomeKind.Cancelled,
            session with { Status = DesktopApprovalSessionStatus.Cancelled },
            isTerminal: true,
            backendStatusCode: null,
            errorMessage: "Approval polling was cancelled.");
    }

    internal static DesktopApprovalOutcome TimedOut(DesktopApprovalSession session)
    {
        return new DesktopApprovalOutcome(
            DesktopApprovalOutcomeKind.TimedOut,
            session,
            isTerminal: true,
            backendStatusCode: null,
            errorMessage: "Approval polling timed out before a terminal backend state was observed.");
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(DesktopApprovalOutcome)} {{ {nameof(Kind)} = {Kind}, {nameof(IsTerminal)} = {IsTerminal}, {nameof(BackendStatusCode)} = {BackendStatusCode} }}";
    }
}
