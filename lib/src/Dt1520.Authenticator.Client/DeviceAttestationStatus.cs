namespace Dt1520.Authenticator.Client;

/// <summary>
/// Server-side attestation state exposed for routing diagnostics.
/// </summary>
public enum DeviceAttestationStatus
{
    /// <summary>
    /// Server returned an unrecognized attestation state.
    /// </summary>
    Unknown,

    /// <summary>
    /// Device did not provide attestation evidence.
    /// </summary>
    NotProvided,

    /// <summary>
    /// Attestation is pending server-side review or processing.
    /// </summary>
    Pending,

    /// <summary>
    /// Attestation was accepted.
    /// </summary>
    Accepted,

    /// <summary>
    /// Attestation was rejected.
    /// </summary>
    Rejected,
}
