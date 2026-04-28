namespace OtpAuth.Application.Administration;

public sealed class AdminSetIntegrationClientActiveStateHandler
{
    private readonly IAdminIntegrationClientStore _store;
    private readonly IAdminIntegrationClientAuditWriter _auditWriter;

    public AdminSetIntegrationClientActiveStateHandler(
        IAdminIntegrationClientStore store,
        IAdminIntegrationClientAuditWriter auditWriter)
    {
        _store = store;
        _auditWriter = auditWriter;
    }

    public async Task<AdminSetIntegrationClientActiveStateResult> HandleAsync(
        AdminIntegrationClientRouteRequest request,
        bool isActive,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.IntegrationClientsWrite))
        {
            return AdminSetIntegrationClientActiveStateResult.Failure(
                AdminSetIntegrationClientActiveStateErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.IntegrationClientsWrite}' is required.");
        }

        var routeError = ValidateRoute(request, out var normalizedClientId);
        if (routeError is not null)
        {
            return AdminSetIntegrationClientActiveStateResult.Failure(
                AdminSetIntegrationClientActiveStateErrorCode.ValidationFailed,
                routeError);
        }

        var existingClient = await _store.GetByTenantAndClientIdAsync(
            request.TenantId,
            normalizedClientId!,
            cancellationToken);
        if (existingClient is null)
        {
            return AdminSetIntegrationClientActiveStateResult.Failure(
                AdminSetIntegrationClientActiveStateErrorCode.NotFound,
                "Integration client was not found.");
        }

        if (existingClient.Status == ToStatus(isActive))
        {
            return AdminSetIntegrationClientActiveStateResult.Failure(
                AdminSetIntegrationClientActiveStateErrorCode.Conflict,
                isActive
                    ? "Integration client is already active."
                    : "Integration client is already inactive.");
        }

        var changedAtUtc = DateTimeOffset.UtcNow;
        var client = await _store.SetIsActiveAsync(
            request.TenantId,
            normalizedClientId!,
            isActive,
            changedAtUtc,
            cancellationToken);
        if (client is null)
        {
            return AdminSetIntegrationClientActiveStateResult.Failure(
                AdminSetIntegrationClientActiveStateErrorCode.NotFound,
                "Integration client was not found.");
        }

        if (isActive)
        {
            await _auditWriter.WriteReactivatedAsync(adminContext, client, cancellationToken);
        }
        else
        {
            await _auditWriter.WriteDeactivatedAsync(adminContext, client, cancellationToken);
        }

        return AdminSetIntegrationClientActiveStateResult.Success(client);
    }

    private static string? ValidateRoute(
        AdminIntegrationClientRouteRequest request,
        out string? normalizedClientId)
    {
        normalizedClientId = AdminIntegrationClientValidation.NormalizeClientId(request.ClientId);
        if (normalizedClientId is null)
        {
            return "ClientId must be 1-200 characters and contain only letters, digits, '.', '_' or '-'.";
        }

        return request.TenantId == Guid.Empty
            ? "TenantId is required."
            : null;
    }

    private static AdminIntegrationClientStatus ToStatus(bool isActive)
    {
        return isActive
            ? AdminIntegrationClientStatus.Active
            : AdminIntegrationClientStatus.Inactive;
    }
}
