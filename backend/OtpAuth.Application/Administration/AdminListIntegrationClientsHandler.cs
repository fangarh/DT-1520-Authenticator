namespace OtpAuth.Application.Administration;

public sealed class AdminListIntegrationClientsHandler
{
    private readonly IAdminIntegrationClientStore _store;

    public AdminListIntegrationClientsHandler(IAdminIntegrationClientStore store)
    {
        _store = store;
    }

    public async Task<AdminListIntegrationClientsResult> HandleAsync(
        AdminIntegrationClientListRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty)
        {
            return AdminListIntegrationClientsResult.Failure(
                AdminListIntegrationClientsErrorCode.ValidationFailed,
                "TenantId is required.");
        }

        if (!adminContext.HasPermission(AdminPermissions.IntegrationClientsRead))
        {
            return AdminListIntegrationClientsResult.Failure(
                AdminListIntegrationClientsErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.IntegrationClientsRead}' is required.");
        }

        var clients = await _store.ListByTenantAsync(request, cancellationToken);
        if (clients.Count == 0)
        {
            return AdminListIntegrationClientsResult.Failure(
                AdminListIntegrationClientsErrorCode.NotFound,
                $"Integration clients for tenant '{request.TenantId}' were not found.");
        }

        return AdminListIntegrationClientsResult.Success(clients);
    }
}
