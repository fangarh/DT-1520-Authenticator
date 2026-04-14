namespace OtpAuth.Application.Integrations;

public sealed record RevokedIntegrationAccessToken
{
    public required string JwtId { get; init; }

    public required string ClientId { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required DateTimeOffset RevokedAtUtc { get; init; }

    public string? Reason { get; init; }
}
