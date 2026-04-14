namespace OtpAuth.Api.Challenges;

public sealed record ChallengeHttpResponse
{
    public required Guid Id { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string FactorType { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public string? CorrelationId { get; init; }
}
