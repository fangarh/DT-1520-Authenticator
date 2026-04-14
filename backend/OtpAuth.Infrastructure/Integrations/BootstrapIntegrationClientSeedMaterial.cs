namespace OtpAuth.Infrastructure.Integrations;

public sealed record BootstrapIntegrationClientSeedMaterial
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ClientSecretHash { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();
}

internal sealed record BootstrapIntegrationClientSeedBase
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }
}
