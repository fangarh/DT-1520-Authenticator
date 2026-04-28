using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

internal sealed record AdminIntegrationClientPersistenceModel
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required bool IsActive { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }

    public DateTimeOffset? LastSecretRotatedUtc { get; init; }

    public required DateTimeOffset LastAuthStateChangedUtc { get; init; }
}

internal static class AdminIntegrationClientDataMapper
{
    public static AdminIntegrationClientView ToDomainModel(
        AdminIntegrationClientPersistenceModel model,
        IReadOnlyDictionary<string, string[]> scopesByClientId)
    {
        return new AdminIntegrationClientView
        {
            ClientId = model.ClientId,
            TenantId = model.TenantId,
            ApplicationClientId = model.ApplicationClientId,
            Status = model.IsActive
                ? AdminIntegrationClientStatus.Active
                : AdminIntegrationClientStatus.Inactive,
            AllowedScopes = scopesByClientId.TryGetValue(model.ClientId, out var scopes)
                ? scopes
                : [],
            CreatedUtc = model.CreatedUtc,
            UpdatedUtc = model.UpdatedUtc,
            LastSecretRotatedUtc = model.LastSecretRotatedUtc,
            LastAuthStateChangedUtc = model.LastAuthStateChangedUtc,
        };
    }
}
