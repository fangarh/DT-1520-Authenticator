namespace Dt1520.Authenticator.Client;

/// <summary>
/// Protected operation metadata for challenge creation.
/// </summary>
public sealed record ChallengeOperation
{
    /// <summary>
    /// Operation type understood by DT-1520 policy.
    /// </summary>
    public required ChallengeOperationType Type { get; init; }

    /// <summary>
    /// Optional human-readable operation name.
    /// </summary>
    public string? DisplayName { get; init; }
}
