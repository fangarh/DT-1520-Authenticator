namespace Dt1520.Authenticator.Client;

/// <summary>
/// Request used to create a DT-1520 second-factor challenge.
/// </summary>
public sealed record CreateChallengeRequest
{
    /// <summary>
    /// Tenant identifier bound to the integration client.
    /// </summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Application client identifier bound to the integration client.
    /// </summary>
    public required Guid ApplicationClientId { get; init; }

    /// <summary>
    /// User subject for the protected operation.
    /// </summary>
    public required ChallengeSubject Subject { get; init; }

    /// <summary>
    /// Protected operation metadata.
    /// </summary>
    public required ChallengeOperation Operation { get; init; }

    /// <summary>
    /// Preferred factor order. When omitted, server policy chooses an allowed factor.
    /// </summary>
    public IReadOnlyCollection<ChallengeFactorType>? PreferredFactors { get; init; }

    /// <summary>
    /// Optional explicit target device for push challenge routing.
    /// </summary>
    public Guid? TargetDeviceId { get; init; }

    /// <summary>
    /// Optional callback registration.
    /// </summary>
    public ChallengeCallbackRegistration? Callback { get; init; }

    /// <summary>
    /// Optional integrator correlation identifier.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Optional idempotency key sent as the <c>Idempotency-Key</c> HTTP header.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(CreateChallengeRequest)} {{ {nameof(TenantId)} = {TenantId}, {nameof(ApplicationClientId)} = {ApplicationClientId}, {nameof(Operation)} = {Operation.Type}, {nameof(HasCallback)} = {HasCallback} }}";
    }

    private bool HasCallback => Callback is not null;
}
