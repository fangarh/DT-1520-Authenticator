using OtpAuth.Domain.Policy;

namespace OtpAuth.Domain.Challenges;

public sealed record Challenge
{
    public required Guid Id { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }

    public required OperationType OperationType { get; init; }

    public string? OperationDisplayName { get; init; }

    public required FactorType FactorType { get; init; }

    public required ChallengeStatus Status { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public string? CorrelationId { get; init; }

    public Uri? CallbackUrl { get; init; }

    public Challenge MarkApproved()
    {
        EnsurePending();
        return this with { Status = ChallengeStatus.Approved };
    }

    public Challenge MarkFailed()
    {
        EnsurePending();
        return this with { Status = ChallengeStatus.Failed };
    }

    public Challenge MarkExpired()
    {
        EnsurePending();
        return this with { Status = ChallengeStatus.Expired };
    }

    private void EnsurePending()
    {
        if (Status != ChallengeStatus.Pending)
        {
            throw new InvalidOperationException($"Challenge '{Id}' is not pending.");
        }
    }
}
