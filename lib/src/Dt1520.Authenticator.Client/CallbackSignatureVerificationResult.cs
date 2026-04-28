namespace Dt1520.Authenticator.Client;

/// <summary>
/// Result of DT-1520 callback signature verification.
/// </summary>
public sealed record CallbackSignatureVerificationResult
{
    private CallbackSignatureVerificationResult(
        bool isValid,
        CallbackSignatureVerificationFailureKind? failureKind)
    {
        IsValid = isValid;
        FailureKind = failureKind;
    }

    /// <summary>
    /// Indicates whether the callback signature is valid for the supplied payload bytes.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Stable failure reason when <see cref="IsValid"/> is <c>false</c>.
    /// </summary>
    public CallbackSignatureVerificationFailureKind? FailureKind { get; }

    /// <summary>
    /// Creates a successful verification result.
    /// </summary>
    public static CallbackSignatureVerificationResult Valid()
    {
        return new CallbackSignatureVerificationResult(true, null);
    }

    /// <summary>
    /// Creates a failed verification result.
    /// </summary>
    public static CallbackSignatureVerificationResult Failed(CallbackSignatureVerificationFailureKind failureKind)
    {
        return new CallbackSignatureVerificationResult(false, failureKind);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(CallbackSignatureVerificationResult)} {{ {nameof(IsValid)} = {IsValid}, {nameof(FailureKind)} = {FailureKind} }}";
    }
}
