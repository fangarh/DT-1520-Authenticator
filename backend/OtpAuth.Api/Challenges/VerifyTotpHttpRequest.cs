namespace OtpAuth.Api.Challenges;

public sealed record VerifyTotpHttpRequest
{
    public required string Code { get; init; }
}
