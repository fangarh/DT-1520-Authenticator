namespace OtpAuth.Application.Integrations;

public sealed record IssuedAccessToken
{
    public required string AccessToken { get; init; }

    public required string TokenType { get; init; }

    public required int ExpiresIn { get; init; }

    public required string Scope { get; init; }
}
