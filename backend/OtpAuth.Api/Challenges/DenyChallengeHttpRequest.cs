namespace OtpAuth.Api.Challenges;

public sealed record DenyChallengeHttpRequest
{
    public Guid? DeviceId { get; init; }

    public string? Reason { get; init; }
}
