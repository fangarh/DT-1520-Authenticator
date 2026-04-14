using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Challenges;

public sealed record CreateChallengeRequest
{
    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }

    public required OperationType OperationType { get; init; }

    public string? OperationDisplayName { get; init; }

    public IReadOnlyCollection<FactorType> PreferredFactors { get; init; } = Array.Empty<FactorType>();

    public string? CorrelationId { get; init; }

    public Uri? CallbackUrl { get; init; }
}
