using OtpAuth.Application.Webhooks;

namespace OtpAuth.Application.Administration;

public sealed class AdminListWebhookSubscriptionsHandler
{
    private readonly WebhookSubscriptionBootstrapService _service;
    private readonly IAdminApplicationClientResolver _applicationClientResolver;

    public AdminListWebhookSubscriptionsHandler(
        WebhookSubscriptionBootstrapService service,
        IAdminApplicationClientResolver applicationClientResolver)
    {
        _service = service;
        _applicationClientResolver = applicationClientResolver;
    }

    public async Task<AdminListWebhookSubscriptionsResult> HandleAsync(
        Guid tenantId,
        Guid? applicationClientId,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return AdminListWebhookSubscriptionsResult.Failure(
                AdminListWebhookSubscriptionsErrorCode.ValidationFailed,
                "TenantId is required.");
        }

        if (!adminContext.HasPermission(AdminPermissions.WebhooksRead))
        {
            return AdminListWebhookSubscriptionsResult.Failure(
                AdminListWebhookSubscriptionsErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.WebhooksRead}' is required.");
        }

        if (applicationClientId is Guid requestedApplicationClientId)
        {
            var resolution = await _applicationClientResolver.ResolveAsync(
                tenantId,
                requestedApplicationClientId,
                cancellationToken);
            if (!resolution.IsSuccess)
            {
                return AdminListWebhookSubscriptionsResult.Failure(
                    AdminListWebhookSubscriptionsErrorCode.NotFound,
                    resolution.ErrorMessage ?? "Application client resolution failed.");
            }
        }

        var subscriptions = await _service.ListAsync(
            new WebhookSubscriptionListRequest
            {
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
            },
            cancellationToken);

        return AdminListWebhookSubscriptionsResult.Success(subscriptions);
    }
}
