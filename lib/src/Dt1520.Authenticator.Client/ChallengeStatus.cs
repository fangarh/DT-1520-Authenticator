namespace Dt1520.Authenticator.Client;

/// <summary>
/// DT-1520 challenge lifecycle status.
/// </summary>
public enum ChallengeStatus
{
    /// <summary>
    /// Challenge is waiting for a second-factor decision.
    /// </summary>
    Pending,

    /// <summary>
    /// Challenge was approved.
    /// </summary>
    Approved,

    /// <summary>
    /// Challenge was denied by the user or device.
    /// </summary>
    Denied,

    /// <summary>
    /// Challenge expired before approval.
    /// </summary>
    Expired,

    /// <summary>
    /// Challenge failed verification.
    /// </summary>
    Failed,
}
