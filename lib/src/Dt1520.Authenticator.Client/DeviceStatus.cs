namespace Dt1520.Authenticator.Client;

/// <summary>
/// Server-side lifecycle state for a registered device.
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// Server returned an unrecognized status.
    /// </summary>
    Unknown,

    /// <summary>
    /// Device exists but is not active yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Device is active.
    /// </summary>
    Active,

    /// <summary>
    /// Device was revoked.
    /// </summary>
    Revoked,

    /// <summary>
    /// Device was blocked by server-side security policy.
    /// </summary>
    Blocked,
}
