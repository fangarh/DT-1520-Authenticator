using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminIntegrationClientDataMapperTests
{
    [Fact]
    public void ToDomainModel_MapsSanitizedClientMetadata()
    {
        var model = new AdminIntegrationClientPersistenceModel
        {
            ClientId = "otpauth-crm",
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ApplicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            IsActive = false,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
            UpdatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            LastSecretRotatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow.AddHours(-12),
        };

        var client = AdminIntegrationClientDataMapper.ToDomainModel(
            model,
            new Dictionary<string, string[]>
            {
                [model.ClientId] =
                [
                    IntegrationClientScopes.DevicesWrite,
                    IntegrationClientScopes.ChallengesRead,
                ],
            });

        Assert.Equal(model.ClientId, client.ClientId);
        Assert.Equal(model.TenantId, client.TenantId);
        Assert.Equal(model.ApplicationClientId, client.ApplicationClientId);
        Assert.Equal(AdminIntegrationClientStatus.Inactive, client.Status);
        Assert.Equal(
            [IntegrationClientScopes.DevicesWrite, IntegrationClientScopes.ChallengesRead],
            client.AllowedScopes);
        Assert.Equal(model.CreatedUtc, client.CreatedUtc);
        Assert.Equal(model.UpdatedUtc, client.UpdatedUtc);
        Assert.Equal(model.LastSecretRotatedUtc, client.LastSecretRotatedUtc);
        Assert.Equal(model.LastAuthStateChangedUtc, client.LastAuthStateChangedUtc);
    }
}
