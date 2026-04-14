namespace OtpAuth.Application.Integrations;

public sealed record IssueIntegrationTokenRequest
{
    public required string GrantType { get; init; }

    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    public string? Scope { get; init; }
}
