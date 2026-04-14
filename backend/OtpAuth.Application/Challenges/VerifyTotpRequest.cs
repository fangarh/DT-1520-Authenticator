namespace OtpAuth.Application.Challenges;

public sealed record VerifyTotpRequest
{
    public required Guid ChallengeId { get; init; }

    public required string Code { get; init; }
}
