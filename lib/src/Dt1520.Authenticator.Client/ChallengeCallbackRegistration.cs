namespace Dt1520.Authenticator.Client;

/// <summary>
/// Callback registration for challenge state changes.
/// </summary>
public sealed record ChallengeCallbackRegistration
{
    /// <summary>
    /// Absolute callback URL owned by the integrating backend.
    /// </summary>
    public required Uri Url { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(ChallengeCallbackRegistration)} {{ {nameof(Url)} = [redacted] }}";
    }
}
