namespace OtpAuth.Application.Integrations;

public sealed record IntegrationClient
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ClientSecretHash { get; init; }

    public DateTimeOffset? LastSecretRotatedUtc { get; init; }

    public DateTimeOffset LastAuthStateChangedUtc { get; init; } = DateTimeOffset.MinValue;

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();
}
