namespace OtpAuth.Application.Challenges;

public sealed record ChallengeAttemptRecord
{
    public required Guid ChallengeId { get; init; }

    public required string AttemptType { get; init; }

    public required string Result { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}
