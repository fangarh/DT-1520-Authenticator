namespace Dt1520.Authenticator.Client;

/// <summary>
/// Stable SDK error category for expected DT-1520 responses and transport failures.
/// </summary>
public enum Dt1520AuthenticatorErrorKind
{
    /// <summary>
    /// Request validation failed or DT-1520 returned HTTP 400.
    /// </summary>
    ValidationFailed,

    /// <summary>
    /// DT-1520 rejected authentication or returned HTTP 401.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// DT-1520 rejected authorization or returned HTTP 403.
    /// </summary>
    Forbidden,

    /// <summary>
    /// The requested DT-1520 resource was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// DT-1520 rejected the operation because of a conflicting state.
    /// </summary>
    Conflict,

    /// <summary>
    /// DT-1520 rate limiting was triggered.
    /// </summary>
    RateLimited,

    /// <summary>
    /// DT-1520 returned a server-side failure.
    /// </summary>
    ServerFailure,

    /// <summary>
    /// The request timed out before a DT-1520 response was received.
    /// </summary>
    Timeout,

    /// <summary>
    /// The caller canceled the operation.
    /// </summary>
    Canceled,

    /// <summary>
    /// A network or protocol failure happened before an expected DT-1520 response was received.
    /// </summary>
    TransportFailure,
}
