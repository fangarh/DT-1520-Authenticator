namespace OtpAuth.Api.Auth;

public sealed record IntrospectIntegrationTokenFormRequest
{
    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public string? Token { get; init; }

    public string? TokenTypeHint { get; init; }
}
