using System.Text;

namespace Dt1520.Authenticator.Client;

/// <summary>
/// Options for framework-agnostic DT-1520 callback signature verification.
/// </summary>
public sealed class CallbackSignatureVerifierOptions
{
    /// <summary>
    /// Shared callback signing secret configured for DT-1520 callback or webhook delivery.
    /// </summary>
    public required string SigningSecret { get; init; }

    /// <summary>
    /// Allowed difference between the supplied callback timestamp and current time.
    /// Timestamp validation is applied only when a timestamp header value is supplied.
    /// </summary>
    public TimeSpan TimestampTolerance { get; init; } = TimeSpan.FromMinutes(5);

    internal byte[] GetSigningSecretBytes()
    {
        if (string.IsNullOrWhiteSpace(SigningSecret))
        {
            throw new ArgumentException("SigningSecret is required.", nameof(SigningSecret));
        }

        if (TimestampTolerance < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(TimestampTolerance), "TimestampTolerance must not be negative.");
        }

        return Encoding.UTF8.GetBytes(SigningSecret);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(CallbackSignatureVerifierOptions)} {{ {nameof(SigningSecret)} = [redacted], {nameof(TimestampTolerance)} = {TimestampTolerance} }}";
    }
}
