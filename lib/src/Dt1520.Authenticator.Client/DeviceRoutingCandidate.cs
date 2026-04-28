namespace Dt1520.Authenticator.Client;

/// <summary>
/// Sanitized device metadata returned for integration-side push routing.
/// </summary>
public sealed record DeviceRoutingCandidate
{
    /// <summary>
    /// Server-side device identifier that can be used as a push challenge target.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Device platform.
    /// </summary>
    public required DevicePlatform Platform { get; init; }

    /// <summary>
    /// Current server-side lifecycle status.
    /// </summary>
    public required DeviceStatus Status { get; init; }

    /// <summary>
    /// Server-side attestation state.
    /// </summary>
    public required DeviceAttestationStatus AttestationStatus { get; init; }

    /// <summary>
    /// Optional operator or user-facing device label returned by DT-1520.
    /// </summary>
    public string? DeviceName { get; init; }

    /// <summary>
    /// Indicates whether DT-1520 currently sees a usable push delivery channel for this device.
    /// </summary>
    public required bool IsPushCapable { get; init; }

    /// <summary>
    /// Device activation timestamp.
    /// </summary>
    public DateTimeOffset? ActivatedAt { get; init; }

    /// <summary>
    /// Last server-observed device activity timestamp.
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; init; }

    /// <summary>
    /// Revocation timestamp when applicable.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>
    /// Block timestamp when applicable.
    /// </summary>
    public DateTimeOffset? BlockedAt { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(DeviceRoutingCandidate)} {{ {nameof(Id)} = {Id}, {nameof(Platform)} = {Platform}, {nameof(Status)} = {Status}, {nameof(IsPushCapable)} = {IsPushCapable} }}";
    }
}
