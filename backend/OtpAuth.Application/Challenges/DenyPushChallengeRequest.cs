namespace OtpAuth.Application.Challenges;

public sealed record DenyPushChallengeRequest
{
    public required Guid ChallengeId { get; init; }

    public Guid? DeviceId { get; init; }

    public string? Reason { get; init; }
}
