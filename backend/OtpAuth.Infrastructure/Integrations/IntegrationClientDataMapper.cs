using Riok.Mapperly.Abstractions;

namespace OtpAuth.Infrastructure.Integrations;

internal sealed record IntegrationClientRecord
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ClientSecretHash { get; init; }
}

internal sealed record IntegrationClientMaterial
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ClientSecretHash { get; init; }
}

[Mapper]
internal static partial class IntegrationClientDataMapper
{
    public static partial IntegrationClientMaterial ToMaterial(IntegrationClientRecord source);
}
