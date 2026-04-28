namespace Dt1520.Authenticator.Desktop;

/// <summary>
/// Local desktop polling outcome.
/// </summary>
public enum DesktopApprovalOutcomeKind
{
    /// <summary>
    /// Polling has not reached a terminal remote state.
    /// </summary>
    Waiting,

    /// <summary>
    /// The integrator backend reported an approved result.
    /// </summary>
    Approved,

    /// <summary>
    /// The integrator backend reported a denied result.
    /// </summary>
    Denied,

    /// <summary>
    /// The integrator backend reported or the session timestamp indicated expiry.
    /// </summary>
    Expired,

    /// <summary>
    /// The integrator backend reported failure or polling could not safely continue.
    /// </summary>
    Failed,

    /// <summary>
    /// The caller cancelled the local desktop wait.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The local polling timeout elapsed before a terminal backend state was observed.
    /// </summary>
    TimedOut,
}
