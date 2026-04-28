namespace Dt1520.Authenticator.Client;

/// <summary>
/// User subject for a DT-1520 challenge.
/// </summary>
public sealed record ChallengeSubject
{
    /// <summary>
    /// Stable external user identifier from the integrating system.
    /// </summary>
    public required string ExternalUserId { get; init; }

    /// <summary>
    /// Optional display username.
    /// </summary>
    public string? Username { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(ChallengeSubject)} {{ {nameof(ExternalUserId)} = [redacted], {nameof(HasUsername)} = {HasUsername} }}";
    }

    private bool HasUsername => !string.IsNullOrWhiteSpace(Username);
}
