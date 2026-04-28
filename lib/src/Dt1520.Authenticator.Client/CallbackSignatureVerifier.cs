using System.Globalization;
using System.Security.Cryptography;

namespace Dt1520.Authenticator.Client;

/// <summary>
/// Verifies DT-1520 callback and webhook signatures over the original request body bytes.
/// </summary>
public sealed class CallbackSignatureVerifier
{
    /// <summary>
    /// Header used by DT-1520 callback and webhook delivery for the HMAC signature.
    /// </summary>
    public const string SignatureHeaderName = "X-OTPAuth-Signature";

    /// <summary>
    /// Optional timestamp header name supported by the SDK when an integrator adds replay-window validation.
    /// The current DT-1520 callback contract does not require this header.
    /// </summary>
    public const string TimestampHeaderName = "X-OTPAuth-Timestamp";

    private const string SupportedAlgorithm = "sha256";
    private const int Sha256SignatureLength = 32;
    private readonly byte[] _signingSecretBytes;
    private readonly TimeSpan _timestampTolerance;

    /// <summary>
    /// Creates a callback signature verifier using a shared signing secret.
    /// </summary>
    public CallbackSignatureVerifier(string signingSecret)
        : this(new CallbackSignatureVerifierOptions { SigningSecret = signingSecret })
    {
    }

    /// <summary>
    /// Creates a callback signature verifier using explicit options.
    /// </summary>
    public CallbackSignatureVerifier(CallbackSignatureVerifierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _signingSecretBytes = options.GetSigningSecretBytes();
        _timestampTolerance = options.TimestampTolerance;
    }

    /// <summary>
    /// Verifies the supplied signature against the original request body bytes.
    /// </summary>
    /// <param name="payloadBytes">Original callback request body bytes. Do not pass reserialized JSON.</param>
    /// <param name="signatureHeaderValue">Value of <c>X-OTPAuth-Signature</c>, for example <c>sha256=&lt;hex&gt;</c>.</param>
    /// <param name="timestampHeaderValue">Optional timestamp header value, either Unix seconds or ISO-8601.</param>
    /// <param name="nowUtc">Optional current time override for deterministic tests.</param>
    public CallbackSignatureVerificationResult Verify(
        ReadOnlySpan<byte> payloadBytes,
        string? signatureHeaderValue,
        string? timestampHeaderValue = null,
        DateTimeOffset? nowUtc = null)
    {
        if (!TryParseSignature(signatureHeaderValue, out var providedSignature, out var failureKind))
        {
            return CallbackSignatureVerificationResult.Failed(failureKind);
        }

        if (!IsTimestampAccepted(timestampHeaderValue, nowUtc ?? DateTimeOffset.UtcNow, out failureKind))
        {
            return CallbackSignatureVerificationResult.Failed(failureKind);
        }

        var expectedSignature = HMACSHA256.HashData(_signingSecretBytes, payloadBytes);
        var isValid = CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature);
        Array.Clear(expectedSignature);
        Array.Clear(providedSignature);

        return isValid
            ? CallbackSignatureVerificationResult.Valid()
            : CallbackSignatureVerificationResult.Failed(CallbackSignatureVerificationFailureKind.SignatureMismatch);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(CallbackSignatureVerifier)} {{ {nameof(SignatureHeaderName)} = {SignatureHeaderName}, {nameof(TimestampHeaderName)} = {TimestampHeaderName} }}";
    }

    private static bool TryParseSignature(
        string? signatureHeaderValue,
        out byte[] signatureBytes,
        out CallbackSignatureVerificationFailureKind failureKind)
    {
        signatureBytes = [];
        failureKind = CallbackSignatureVerificationFailureKind.InvalidFormat;

        if (string.IsNullOrWhiteSpace(signatureHeaderValue))
        {
            failureKind = CallbackSignatureVerificationFailureKind.MissingSignature;
            return false;
        }

        var separatorIndex = signatureHeaderValue.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == signatureHeaderValue.Length - 1)
        {
            failureKind = CallbackSignatureVerificationFailureKind.InvalidFormat;
            return false;
        }

        var algorithm = signatureHeaderValue[..separatorIndex].Trim();
        if (!string.Equals(algorithm, SupportedAlgorithm, StringComparison.OrdinalIgnoreCase))
        {
            failureKind = CallbackSignatureVerificationFailureKind.UnsupportedAlgorithm;
            return false;
        }

        var hexSignature = signatureHeaderValue[(separatorIndex + 1)..].Trim();
        if (hexSignature.Length != Sha256SignatureLength * 2)
        {
            failureKind = CallbackSignatureVerificationFailureKind.InvalidFormat;
            return false;
        }

        try
        {
            signatureBytes = Convert.FromHexString(hexSignature);
            return true;
        }
        catch (FormatException)
        {
            failureKind = CallbackSignatureVerificationFailureKind.InvalidFormat;
            return false;
        }
    }

    private bool IsTimestampAccepted(
        string? timestampHeaderValue,
        DateTimeOffset nowUtc,
        out CallbackSignatureVerificationFailureKind failureKind)
    {
        failureKind = CallbackSignatureVerificationFailureKind.InvalidFormat;

        if (string.IsNullOrWhiteSpace(timestampHeaderValue))
        {
            return true;
        }

        if (!TryParseTimestamp(timestampHeaderValue, out var timestampUtc))
        {
            failureKind = CallbackSignatureVerificationFailureKind.InvalidFormat;
            return false;
        }

        var age = nowUtc.ToUniversalTime() - timestampUtc.ToUniversalTime();
        if (age.Duration() <= _timestampTolerance)
        {
            return true;
        }

        failureKind = CallbackSignatureVerificationFailureKind.TimestampOutsideTolerance;
        return false;
    }

    private static bool TryParseTimestamp(string value, out DateTimeOffset timestampUtc)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            try
            {
                timestampUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                timestampUtc = default;
                return false;
            }
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestampUtc);
    }
}
