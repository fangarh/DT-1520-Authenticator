namespace Dt1520.Authenticator.Client;

/// <summary>
/// Request used to verify a TOTP code against an active challenge.
/// </summary>
public sealed record VerifyTotpRequest
{
    /// <summary>
    /// Six-digit TOTP code.
    /// </summary>
    public required string Code { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(VerifyTotpRequest)} {{ {nameof(Code)} = [redacted] }}";
    }
}
