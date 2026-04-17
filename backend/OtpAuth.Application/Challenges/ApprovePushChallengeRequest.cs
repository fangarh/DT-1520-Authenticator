namespace OtpAuth.Application.Challenges;

public sealed record ApprovePushChallengeRequest
{
    public required Guid ChallengeId { get; init; }

    public required Guid DeviceId { get; init; }

    public required bool BiometricVerified { get; init; }
}
