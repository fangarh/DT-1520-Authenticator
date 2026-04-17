namespace OtpAuth.Application.Challenges;

public sealed record VerifyBackupCodeRequest
{
    public required Guid ChallengeId { get; init; }

    public required string Code { get; init; }
}
