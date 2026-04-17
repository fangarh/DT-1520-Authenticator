namespace OtpAuth.Application.Administration;

public sealed record AdminLoginAttemptKey
{
    public required string NormalizedUsername { get; init; }

    public required string RemoteAddress { get; init; }
}
