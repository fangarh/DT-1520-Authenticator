using OtpAuth.Application.Webhooks;

namespace OtpAuth.Application.Administration;

public interface IAdminWebhookSubscriptionAuditWriter
{
    Task WriteSavedAsync(
        AdminContext adminContext,
        WebhookSubscription subscription,
        CancellationToken cancellationToken);
}
