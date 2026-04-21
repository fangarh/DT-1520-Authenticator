using OtpAuth.Application.Webhooks;

namespace OtpAuth.Application.Administration;

public sealed class AdminUpsertWebhookSubscriptionHandler
{
    private readonly WebhookSubscriptionBootstrapService _service;
    private readonly IAdminApplicationClientResolver _applicationClientResolver;
    private readonly IAdminWebhookSubscriptionAuditWriter _auditWriter;

    public AdminUpsertWebhookSubscriptionHandler(
        WebhookSubscriptionBootstrapService service,
        IAdminApplicationClientResolver applicationClientResolver,
        IAdminWebhookSubscriptionAuditWriter auditWriter)
    {
        _service = service;
        _applicationClientResolver = applicationClientResolver;
        _auditWriter = auditWriter;
    }

    public async Task<AdminUpsertWebhookSubscriptionResult> HandleAsync(
        AdminWebhookSubscriptionUpsertRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TenantId == Guid.Empty)
        {
            return AdminUpsertWebhookSubscriptionResult.Failure(
                AdminUpsertWebhookSubscriptionErrorCode.ValidationFailed,
                "TenantId is required.");
        }

        if (!adminContext.HasPermission(AdminPermissions.WebhooksWrite))
        {
            return AdminUpsertWebhookSubscriptionResult.Failure(
                AdminUpsertWebhookSubscriptionErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.WebhooksWrite}' is required.");
        }

        var resolution = await _applicationClientResolver.ResolveAsync(
            request.TenantId,
            request.ApplicationClientId,
            cancellationToken);
        if (!resolution.IsSuccess || resolution.ApplicationClientId is not Guid applicationClientId)
        {
            return AdminUpsertWebhookSubscriptionResult.Failure(
                resolution.ErrorCode == AdminApplicationClientResolutionErrorCode.Conflict
                    ? AdminUpsertWebhookSubscriptionErrorCode.Conflict
                    : AdminUpsertWebhookSubscriptionErrorCode.NotFound,
                resolution.ErrorMessage ?? "Application client resolution failed.");
        }

        try
        {
            var subscription = await _service.UpsertAsync(
                new WebhookSubscriptionUpsertRequest
                {
                    TenantId = request.TenantId,
                    ApplicationClientId = applicationClientId,
                    EndpointUrl = request.EndpointUrl,
                    EventTypes = request.EventTypes,
                    IsActive = request.IsActive,
                },
                cancellationToken);

            await _auditWriter.WriteSavedAsync(adminContext, subscription, cancellationToken);
            return AdminUpsertWebhookSubscriptionResult.Success(subscription);
        }
        catch (InvalidOperationException exception)
        {
            return AdminUpsertWebhookSubscriptionResult.Failure(
                AdminUpsertWebhookSubscriptionErrorCode.ValidationFailed,
                exception.Message);
        }
    }
}
