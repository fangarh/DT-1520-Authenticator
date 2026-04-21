namespace OtpAuth.Application.Administration;

public sealed class AdminListUserDevicesHandler
{
    private readonly IAdminDeviceStore _store;

    public AdminListUserDevicesHandler(IAdminDeviceStore store)
    {
        _store = store;
    }

    public async Task<AdminListUserDevicesResult> HandleAsync(
        AdminUserDeviceListRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        var normalizedExternalUserId = NormalizeExternalUserId(request.ExternalUserId, out var validationError);
        if (validationError is not null)
        {
            return AdminListUserDevicesResult.Failure(
                AdminListUserDevicesErrorCode.ValidationFailed,
                validationError);
        }

        if (request.TenantId == Guid.Empty)
        {
            return AdminListUserDevicesResult.Failure(
                AdminListUserDevicesErrorCode.ValidationFailed,
                "TenantId is required.");
        }

        if (!adminContext.HasPermission(AdminPermissions.DevicesRead))
        {
            return AdminListUserDevicesResult.Failure(
                AdminListUserDevicesErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.DevicesRead}' is required.");
        }

        var devices = await _store.ListByExternalUserAsync(
            request with
            {
                ExternalUserId = normalizedExternalUserId!,
            },
            cancellationToken);
        if (devices.Count == 0)
        {
            return AdminListUserDevicesResult.Failure(
                AdminListUserDevicesErrorCode.NotFound,
                $"Devices for tenant '{request.TenantId}' and external user '{normalizedExternalUserId}' were not found.");
        }

        return AdminListUserDevicesResult.Success(devices);
    }

    private static string? NormalizeExternalUserId(string externalUserId, out string? validationError)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            validationError = "ExternalUserId is required.";
            return null;
        }

        var normalizedExternalUserId = externalUserId.Trim();
        validationError = normalizedExternalUserId.Length > 256
            ? "ExternalUserId must be 256 characters or fewer."
            : null;

        return validationError is null
            ? normalizedExternalUserId
            : null;
    }
}
