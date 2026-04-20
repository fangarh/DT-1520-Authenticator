namespace OtpAuth.Api.Devices;

public sealed record PendingDeviceChallengeHttpResponse
{
    public required Guid Id { get; init; }

    public required string FactorType { get; init; }

    public required string Status { get; init; }

    public required string OperationType { get; init; }

    public string? OperationDisplayName { get; init; }

    public string? Username { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public string? CorrelationId { get; init; }
}
