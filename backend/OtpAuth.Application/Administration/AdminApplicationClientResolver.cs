using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Administration;

public sealed class AdminApplicationClientResolver : IAdminApplicationClientResolver
{
    private readonly IIntegrationClientStore _integrationClientStore;

    public AdminApplicationClientResolver(IIntegrationClientStore integrationClientStore)
    {
        _integrationClientStore = integrationClientStore;
    }

    public async Task<AdminApplicationClientResolutionResult> ResolveAsync(
        Guid tenantId,
        Guid? requestedApplicationClientId,
        CancellationToken cancellationToken)
    {
        var clients = await _integrationClientStore.ListActiveByTenantAsync(tenantId, cancellationToken);
        if (requestedApplicationClientId is Guid applicationClientId)
        {
            return clients.Any(client => client.ApplicationClientId == applicationClientId)
                ? AdminApplicationClientResolutionResult.Success(applicationClientId)
                : AdminApplicationClientResolutionResult.Failure(
                    AdminApplicationClientResolutionErrorCode.NotFound,
                    $"Application client '{applicationClientId}' was not found for tenant '{tenantId}'.");
        }

        return clients.Count switch
        {
            0 => AdminApplicationClientResolutionResult.Failure(
                AdminApplicationClientResolutionErrorCode.NotFound,
                $"Tenant '{tenantId}' has no active application clients."),
            1 => AdminApplicationClientResolutionResult.Success(clients.Single().ApplicationClientId),
            _ => AdminApplicationClientResolutionResult.Failure(
                AdminApplicationClientResolutionErrorCode.Conflict,
                $"Tenant '{tenantId}' has multiple active application clients. Provide ApplicationClientId explicitly."),
        };
    }
}
