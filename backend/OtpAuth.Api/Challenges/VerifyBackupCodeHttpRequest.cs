namespace OtpAuth.Api.Challenges;

public sealed record VerifyBackupCodeHttpRequest
{
    public required string Code { get; init; }
}
