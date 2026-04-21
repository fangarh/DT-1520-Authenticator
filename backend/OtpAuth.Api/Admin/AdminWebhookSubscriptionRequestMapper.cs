using OtpAuth.Application.Administration;
using OtpAuth.Application.Webhooks;

namespace OtpAuth.Api.Admin;

public static class AdminWebhookSubscriptionRequestMapper
{
    public static AdminWebhookSubscriptionUpsertRequest Map(AdminUpsertWebhookSubscriptionHttpRequest request)
    {
        return new AdminWebhookSubscriptionUpsertRequest
        {
            TenantId = request.TenantId,
            ApplicationClientId = request.ApplicationClientId,
            EndpointUrl = request.EndpointUrl,
            EventTypes = request.EventTypes,
            IsActive = request.IsActive,
        };
    }

    public static AdminWebhookSubscriptionHttpResponse MapResponse(WebhookSubscription subscription)
    {
        return new AdminWebhookSubscriptionHttpResponse
        {
            SubscriptionId = subscription.SubscriptionId,
            TenantId = subscription.TenantId,
            ApplicationClientId = subscription.ApplicationClientId,
            EndpointUrl = subscription.EndpointUrl.ToString(),
            Status = subscription.IsActive ? "active" : "inactive",
            EventTypes = subscription.EventTypes
                .OrderBy(static item => item, StringComparer.Ordinal)
                .ToArray(),
            CreatedUtc = subscription.CreatedUtc,
            UpdatedUtc = subscription.UpdatedUtc,
        };
    }
}
