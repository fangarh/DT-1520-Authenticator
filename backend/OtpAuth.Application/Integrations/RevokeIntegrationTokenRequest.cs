namespace OtpAuth.Application.Integrations;

public sealed record RevokeIntegrationTokenRequest
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    public required string Token { get; init; }

    public string? TokenTypeHint { get; init; }
}
