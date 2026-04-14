using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class BootstrapIntegrationClientSeedMaterialFactoryTests
{
    [Fact]
    public void Create_ReturnsSeedMaterial_WhenConfiguredClientIsValid()
    {
        const string envVarName = "OTPAUTH_TEST_BOOTSTRAP_CLIENT_SECRET";
        Environment.SetEnvironmentVariable(envVarName, "super-secret");

        try
        {
            var hasher = new Pbkdf2ClientSecretHasher();
            var factory = new BootstrapIntegrationClientSeedMaterialFactory(hasher);
            var materials = factory.Create(
                new BootstrapOAuthOptions
                {
                    Clients =
                    [
                        new BootstrapOAuthClientOptions
                        {
                            ClientId = "crm-client",
                            TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"),
                            ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"),
                            ClientSecretEnvVarName = envVarName,
                            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
                        },
                    ],
                });

            var client = Assert.Single(materials);

            Assert.Equal("crm-client", client.ClientId);
            Assert.Contains(IntegrationClientScopes.ChallengesRead, client.AllowedScopes);
            Assert.True(hasher.Verify("super-secret", client.ClientSecretHash));
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void Create_Throws_WhenSecretEnvVarIsMissing()
    {
        var factory = new BootstrapIntegrationClientSeedMaterialFactory(new Pbkdf2ClientSecretHasher());

        var error = Assert.Throws<InvalidOperationException>(() => factory.Create(
            new BootstrapOAuthOptions
            {
                Clients =
                [
                    new BootstrapOAuthClientOptions
                    {
                        ClientId = "crm-client",
                        TenantId = Guid.NewGuid(),
                        ApplicationClientId = Guid.NewGuid(),
                        ClientSecretEnvVarName = "OTPAUTH_TEST_MISSING_SECRET",
                        AllowedScopes = [IntegrationClientScopes.ChallengesRead],
                    },
                ],
            }));

        Assert.Contains("OTPAUTH_TEST_MISSING_SECRET", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_Throws_WhenScopesCollapseToEmptySet()
    {
        const string envVarName = "OTPAUTH_TEST_BOOTSTRAP_CLIENT_SECRET_EMPTY_SCOPE";
        Environment.SetEnvironmentVariable(envVarName, "super-secret");

        try
        {
            var factory = new BootstrapIntegrationClientSeedMaterialFactory(new Pbkdf2ClientSecretHasher());

            var error = Assert.Throws<InvalidOperationException>(() => factory.Create(
                new BootstrapOAuthOptions
                {
                    Clients =
                    [
                        new BootstrapOAuthClientOptions
                        {
                            ClientId = "crm-client",
                            TenantId = Guid.NewGuid(),
                            ApplicationClientId = Guid.NewGuid(),
                            ClientSecretEnvVarName = envVarName,
                            AllowedScopes = [" ", ""],
                        },
                    ],
                }));

            Assert.Contains("At least one allowed scope is required", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }
}

public sealed class IntegrationClientDataMapperTests
{
    [Fact]
    public void ToMaterial_MapsRecordFields()
    {
        var record = new IntegrationClientRecord
        {
            ClientId = "crm-client",
            TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"),
            ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"),
            ClientSecretHash = "pbkdf2-sha256$100000$salt$hash",
        };

        var material = IntegrationClientDataMapper.ToMaterial(record);

        Assert.Equal(record.ClientId, material.ClientId);
        Assert.Equal(record.TenantId, material.TenantId);
        Assert.Equal(record.ApplicationClientId, material.ApplicationClientId);
        Assert.Equal(record.ClientSecretHash, material.ClientSecretHash);
    }
}

public sealed class Pbkdf2ClientSecretHasherTests
{
    [Fact]
    public void Verify_ReturnsTrue_ForMatchingSecret()
    {
        var hasher = new Pbkdf2ClientSecretHasher();
        var hash = hasher.Hash("bootstrap-secret");

        var isValid = hasher.Verify("bootstrap-secret", hash);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_ReturnsFalse_ForDifferentSecret()
    {
        var hasher = new Pbkdf2ClientSecretHasher();
        var hash = hasher.Hash("bootstrap-secret");

        var isValid = hasher.Verify("wrong-secret", hash);

        Assert.False(isValid);
    }
}
