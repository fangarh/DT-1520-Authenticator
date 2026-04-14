namespace OtpAuth.Api.Auth;

public sealed record IntrospectIntegrationTokenResponse
{
    public required bool Active { get; init; }

    public string? ClientId { get; init; }

    public Guid? TenantId { get; init; }

    public Guid? ApplicationClientId { get; init; }

    public string? Scope { get; init; }

    public DateTimeOffset? ExpiresAtUtc { get; init; }

    public string? TokenType { get; init; }
}
