namespace OtpAuth.Api.Auth;

public sealed record IssueIntegrationTokenResponse
{
    public required string AccessToken { get; init; }

    public required string TokenType { get; init; }

    public required int ExpiresIn { get; init; }

    public required string Scope { get; init; }
}
