namespace Dt1520.Authenticator.Client;

/// <summary>
/// Sanitized DT-1520 challenge response.
/// </summary>
public sealed record ChallengeResponse
{
    /// <summary>
    /// Challenge identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Application client identifier.
    /// </summary>
    public required Guid ApplicationClientId { get; init; }

    /// <summary>
    /// Selected factor type.
    /// </summary>
    public required ChallengeFactorType FactorType { get; init; }

    /// <summary>
    /// Current challenge status.
    /// </summary>
    public required ChallengeStatus Status { get; init; }

    /// <summary>
    /// Challenge expiry timestamp.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Push target device when the challenge is device-bound.
    /// </summary>
    public Guid? TargetDeviceId { get; init; }

    /// <summary>
    /// Approval timestamp when approved.
    /// </summary>
    public DateTimeOffset? ApprovedAt { get; init; }

    /// <summary>
    /// Denial timestamp when denied.
    /// </summary>
    public DateTimeOffset? DeniedAt { get; init; }

    /// <summary>
    /// Integrator correlation identifier when one was provided.
    /// </summary>
    public string? CorrelationId { get; init; }
}
