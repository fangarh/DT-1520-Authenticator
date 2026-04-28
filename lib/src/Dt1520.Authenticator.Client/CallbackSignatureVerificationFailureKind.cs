namespace Dt1520.Authenticator.Client;

/// <summary>
/// Stable reason why a DT-1520 callback signature could not be validated.
/// </summary>
public enum CallbackSignatureVerificationFailureKind
{
    /// <summary>
    /// No signature header value was provided.
    /// </summary>
    MissingSignature,

    /// <summary>
    /// The signature or timestamp value does not match the expected format.
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// The signature uses an algorithm that is not supported by this SDK version.
    /// </summary>
    UnsupportedAlgorithm,

    /// <summary>
    /// The supplied timestamp is outside the configured tolerance.
    /// </summary>
    TimestampOutsideTolerance,

    /// <summary>
    /// The HMAC does not match the supplied payload bytes.
    /// </summary>
    SignatureMismatch,
}
