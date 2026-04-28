namespace OtpAuth.Application.Administration;

public sealed class AdminListTenantDirectoryHandler
{
    private readonly IAdminTenantDirectoryStore _store;

    public AdminListTenantDirectoryHandler(IAdminTenantDirectoryStore store)
    {
        _store = store;
    }

    public async Task<AdminListTenantDirectoryResult> HandleAsync(
        AdminTenantDirectoryListRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.TenantsRead))
        {
            return AdminListTenantDirectoryResult.Failure(
                AdminListTenantDirectoryErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.TenantsRead}' is required.");
        }

        var tenants = await _store.ListTenantsAsync(cancellationToken);
        return AdminListTenantDirectoryResult.Success(tenants);
    }
}
