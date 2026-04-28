namespace OtpAuth.Application.Administration;

public sealed class AdminGetTenantDirectoryHandler
{
    private readonly IAdminTenantDirectoryStore _store;

    public AdminGetTenantDirectoryHandler(IAdminTenantDirectoryStore store)
    {
        _store = store;
    }

    public async Task<AdminGetTenantDirectoryResult> HandleAsync(
        AdminTenantDirectoryDetailRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.TenantsRead))
        {
            return AdminGetTenantDirectoryResult.Failure(
                AdminGetTenantDirectoryErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.TenantsRead}' is required.");
        }

        if (request.TenantId == Guid.Empty)
        {
            return AdminGetTenantDirectoryResult.Failure(
                AdminGetTenantDirectoryErrorCode.ValidationFailed,
                "TenantId is required.");
        }

        var directory = await _store.GetDirectoryAsync(request.TenantId, cancellationToken);
        return directory is null
            ? AdminGetTenantDirectoryResult.Failure(
                AdminGetTenantDirectoryErrorCode.NotFound,
                $"Tenant '{request.TenantId}' was not found.")
            : AdminGetTenantDirectoryResult.Success(directory);
    }
}
