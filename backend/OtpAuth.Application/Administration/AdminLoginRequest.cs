namespace OtpAuth.Application.Administration;

public sealed record AdminLoginRequest
{
    public required string Username { get; init; }

    public required string Password { get; init; }

    public string? RemoteAddress { get; init; }
}
