using System.Text.Json;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Webhooks;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Administration;

public sealed class AdminWebhookSubscriptionAuditWriter : IAdminWebhookSubscriptionAuditWriter
{
    private readonly SecurityAuditService _securityAuditService;

    public AdminWebhookSubscriptionAuditWriter(SecurityAuditService securityAuditService)
    {
        _securityAuditService = securityAuditService;
    }

    public Task WriteSavedAsync(
        AdminContext adminContext,
        WebhookSubscription subscription,
        CancellationToken cancellationToken)
    {
        var eventType = subscription.IsActive
            ? "admin_webhook_subscription.upserted"
            : "admin_webhook_subscription.deactivated";
        var summary = subscription.IsActive
            ? "Admin saved webhook subscription."
            : "Admin deactivated webhook subscription.";

        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = eventType,
                SubjectType = "webhook_subscription",
                SubjectId = subscription.SubscriptionId.ToString("D"),
                Summary = summary,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId = adminContext.AdminUserId,
                    adminUsername = adminContext.Username,
                    action = new
                    {
                        subscriptionId = subscription.SubscriptionId,
                        tenantId = subscription.TenantId,
                        applicationClientId = subscription.ApplicationClientId,
                        endpointUrl = subscription.EndpointUrl,
                        status = subscription.IsActive ? "active" : "inactive",
                        eventTypes = subscription.EventTypes.OrderBy(static item => item, StringComparer.Ordinal),
                    },
                }),
                Severity = "info",
                Source = "admin_api",
            },
            cancellationToken);
    }
}
