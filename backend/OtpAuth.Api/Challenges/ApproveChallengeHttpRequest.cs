namespace OtpAuth.Api.Challenges;

public sealed record ApproveChallengeHttpRequest
{
    public required Guid DeviceId { get; init; }

    public bool BiometricVerified { get; init; }
}
