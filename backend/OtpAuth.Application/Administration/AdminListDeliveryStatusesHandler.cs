namespace OtpAuth.Application.Administration;

public sealed class AdminListDeliveryStatusesHandler
{
    private const int MaxLimit = 200;

    private readonly IAdminDeliveryStatusStore _store;
    private readonly IAdminApplicationClientResolver _applicationClientResolver;

    public AdminListDeliveryStatusesHandler(
        IAdminDeliveryStatusStore store,
        IAdminApplicationClientResolver applicationClientResolver)
    {
        _store = store;
        _applicationClientResolver = applicationClientResolver;
    }

    public async Task<AdminListDeliveryStatusesResult> HandleAsync(
        AdminDeliveryStatusListRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return AdminListDeliveryStatusesResult.Failure(
                AdminListDeliveryStatusesErrorCode.ValidationFailed,
                validationError);
        }

        if (!adminContext.HasPermission(AdminPermissions.WebhooksRead))
        {
            return AdminListDeliveryStatusesResult.Failure(
                AdminListDeliveryStatusesErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.WebhooksRead}' is required.");
        }

        if (request.ApplicationClientId is Guid requestedApplicationClientId)
        {
            var resolution = await _applicationClientResolver.ResolveAsync(
                request.TenantId,
                requestedApplicationClientId,
                cancellationToken);
            if (!resolution.IsSuccess)
            {
                return AdminListDeliveryStatusesResult.Failure(
                    AdminListDeliveryStatusesErrorCode.NotFound,
                    resolution.ErrorMessage ?? "Application client resolution failed.");
            }
        }

        var deliveries = await _store.ListRecentAsync(request, cancellationToken);
        return AdminListDeliveryStatusesResult.Success(deliveries);
    }

    private static string? Validate(AdminDeliveryStatusListRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (request.ApplicationClientId == Guid.Empty)
        {
            return "ApplicationClientId must not be empty when provided.";
        }

        return request.Limit is < 1 or > MaxLimit
            ? $"Limit must be between 1 and {MaxLimit}."
            : null;
    }
}
