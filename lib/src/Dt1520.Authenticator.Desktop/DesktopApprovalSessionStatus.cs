namespace Dt1520.Authenticator.Desktop;

/// <summary>
/// Desktop-facing approval session state.
/// </summary>
public enum DesktopApprovalSessionStatus
{
    /// <summary>
    /// The approval is still waiting for user action or backend completion.
    /// </summary>
    Waiting,

    /// <summary>
    /// The protected operation was approved.
    /// </summary>
    Approved,

    /// <summary>
    /// The protected operation was denied.
    /// </summary>
    Denied,

    /// <summary>
    /// The approval session expired.
    /// </summary>
    Expired,

    /// <summary>
    /// The approval session failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The local desktop wait was cancelled.
    /// </summary>
    Cancelled,
}
