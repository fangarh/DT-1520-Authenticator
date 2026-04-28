namespace OtpAuth.Application.Administration;

public sealed class AdminUpdateIntegrationClientScopesHandler
{
    private readonly IAdminIntegrationClientStore _store;
    private readonly IAdminIntegrationClientAuditWriter _auditWriter;

    public AdminUpdateIntegrationClientScopesHandler(
        IAdminIntegrationClientStore store,
        IAdminIntegrationClientAuditWriter auditWriter)
    {
        _store = store;
        _auditWriter = auditWriter;
    }

    public async Task<AdminUpdateIntegrationClientScopesResult> HandleAsync(
        AdminIntegrationClientUpdateScopesRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.IntegrationClientsWrite))
        {
            return AdminUpdateIntegrationClientScopesResult.Failure(
                AdminUpdateIntegrationClientScopesErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.IntegrationClientsWrite}' is required.");
        }

        var routeError = ValidateRoute(request.TenantId, request.ClientId, out var normalizedClientId);
        if (routeError is not null)
        {
            return AdminUpdateIntegrationClientScopesResult.Failure(
                AdminUpdateIntegrationClientScopesErrorCode.ValidationFailed,
                routeError);
        }

        var normalizedScopes = AdminIntegrationClientValidation.NormalizeScopes(request.AllowedScopes, out var scopeError);
        if (scopeError is not null)
        {
            return AdminUpdateIntegrationClientScopesResult.Failure(
                AdminUpdateIntegrationClientScopesErrorCode.ValidationFailed,
                scopeError);
        }

        var changedAtUtc = DateTimeOffset.UtcNow;
        var client = await _store.UpdateScopesAsync(
            request.TenantId,
            normalizedClientId!,
            normalizedScopes,
            changedAtUtc,
            cancellationToken);
        if (client is null)
        {
            return AdminUpdateIntegrationClientScopesResult.Failure(
                AdminUpdateIntegrationClientScopesErrorCode.NotFound,
                "Integration client was not found.");
        }

        await _auditWriter.WriteScopesChangedAsync(adminContext, client, cancellationToken);
        return AdminUpdateIntegrationClientScopesResult.Success(client);
    }

    private static string? ValidateRoute(Guid tenantId, string clientId, out string? normalizedClientId)
    {
        normalizedClientId = AdminIntegrationClientValidation.NormalizeClientId(clientId);
        if (normalizedClientId is null)
        {
            return "ClientId must be 1-200 characters and contain only letters, digits, '.', '_' or '-'.";
        }

        return tenantId == Guid.Empty
            ? "TenantId is required."
            : null;
    }
}
