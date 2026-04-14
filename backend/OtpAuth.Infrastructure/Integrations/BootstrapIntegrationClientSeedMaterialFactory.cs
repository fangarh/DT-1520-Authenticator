using OtpAuth.Application.Integrations;
using Riok.Mapperly.Abstractions;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class BootstrapIntegrationClientSeedMaterialFactory
{
    private readonly IClientSecretHasher _clientSecretHasher;

    public BootstrapIntegrationClientSeedMaterialFactory(IClientSecretHasher clientSecretHasher)
    {
        _clientSecretHasher = clientSecretHasher;
    }

    public IReadOnlyCollection<BootstrapIntegrationClientSeedMaterial> Create(BootstrapOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var materials = new List<BootstrapIntegrationClientSeedMaterial>();

        foreach (var client in options.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
            {
                throw new InvalidOperationException("Bootstrap OAuth client id is required.");
            }

            if (string.IsNullOrWhiteSpace(client.ClientSecretEnvVarName))
            {
                throw new InvalidOperationException(
                    $"Environment variable name is required for bootstrap client '{client.ClientId}'.");
            }

            var clientSecret = Environment.GetEnvironmentVariable(client.ClientSecretEnvVarName);
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{client.ClientSecretEnvVarName}' is required to seed bootstrap client '{client.ClientId}'.");
            }

            var allowedScopes = NormalizeScopes(client.AllowedScopes);
            if (allowedScopes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"At least one allowed scope is required for bootstrap client '{client.ClientId}'.");
            }

            var seedBase = BootstrapIntegrationClientSeedMapper.ToSeedBase(client);
            materials.Add(new BootstrapIntegrationClientSeedMaterial
            {
                ClientId = seedBase.ClientId,
                TenantId = seedBase.TenantId,
                ApplicationClientId = seedBase.ApplicationClientId,
                ClientSecretHash = _clientSecretHasher.Hash(clientSecret),
                AllowedScopes = allowedScopes,
            });
        }

        return materials;
    }

    private static string[] NormalizeScopes(IReadOnlyCollection<string> scopes)
    {
        return scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}

[Mapper]
internal static partial class BootstrapIntegrationClientSeedMapper
{
    [MapperIgnoreSource(nameof(BootstrapOAuthClientOptions.ClientSecretEnvVarName))]
    [MapperIgnoreSource(nameof(BootstrapOAuthClientOptions.AllowedScopes))]
    public static partial BootstrapIntegrationClientSeedBase ToSeedBase(BootstrapOAuthClientOptions source);
}
