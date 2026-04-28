namespace Dt1520.Authenticator.Client;

/// <summary>
/// Normalized push challenge outcome.
/// </summary>
public enum PushChallengeOutcomeKind
{
    /// <summary>
    /// Challenge is still waiting for a device decision.
    /// </summary>
    Pending,

    /// <summary>
    /// User approved the challenge.
    /// </summary>
    Approved,

    /// <summary>
    /// User denied the challenge.
    /// </summary>
    Denied,

    /// <summary>
    /// Challenge expired before a decision.
    /// </summary>
    Expired,

    /// <summary>
    /// Challenge failed.
    /// </summary>
    Failed,
}
