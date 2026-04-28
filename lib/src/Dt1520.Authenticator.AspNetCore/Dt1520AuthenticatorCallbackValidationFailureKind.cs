namespace Dt1520.Authenticator.AspNetCore;

/// <summary>
/// Stable failure reasons returned by ASP.NET Core callback validation helpers.
/// </summary>
public enum Dt1520AuthenticatorCallbackValidationFailureKind
{
    /// <summary>
    /// The callback body exceeded the configured safe read limit.
    /// </summary>
    BodyTooLarge,

    /// <summary>
    /// The callback body could not be read before signature validation.
    /// </summary>
    BodyReadFailed,

    /// <summary>
    /// The signature header was missing.
    /// </summary>
    MissingSignature,

    /// <summary>
    /// The signature or timestamp header format was invalid.
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// The signature algorithm is not supported.
    /// </summary>
    UnsupportedAlgorithm,

    /// <summary>
    /// The optional timestamp was outside the accepted tolerance.
    /// </summary>
    TimestampOutsideTolerance,

    /// <summary>
    /// The supplied signature did not match the original callback body bytes.
    /// </summary>
    SignatureMismatch,
}
