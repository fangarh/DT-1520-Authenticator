using OtpAuth.Application.Webhooks;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Webhooks;

public sealed class WebhookSubscriptionBootstrapServiceTests
{
    [Fact]
    public async Task UpsertAsync_NormalizesDistinctEventTypes_AndPersistsSubscription()
    {
        var store = new StubWebhookSubscriptionStore();
        var service = new WebhookSubscriptionBootstrapService(store);
        var request = new WebhookSubscriptionUpsertRequest
        {
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            EndpointUrl = new Uri("https://crm.example.com/webhooks/otpauth"),
            EventTypes =
            [
                WebhookEventTypeNames.ChallengeDenied,
                " challenge.approved ",
                WebhookEventTypeNames.ChallengeDenied,
            ],
        };

        var subscription = await service.UpsertAsync(request, CancellationToken.None);

        Assert.Equal(
            [WebhookEventTypeNames.ChallengeApproved, WebhookEventTypeNames.ChallengeDenied],
            subscription.EventTypes);
    }

    [Fact]
    public async Task UpsertAsync_RejectsPrivateNetworkEndpoint()
    {
        var service = new WebhookSubscriptionBootstrapService(new StubWebhookSubscriptionStore());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertAsync(
            new WebhookSubscriptionUpsertRequest
            {
                TenantId = Guid.NewGuid(),
                ApplicationClientId = Guid.NewGuid(),
                EndpointUrl = new Uri("https://127.0.0.1/webhooks"),
                EventTypes = [WebhookEventTypeNames.ChallengeApproved],
            },
            CancellationToken.None));

        Assert.Equal(
            "Webhook endpoint must not target localhost or private network IP literals.",
            exception.Message);
    }

    [Fact]
    public async Task UpsertAsync_RejectsUnsupportedEventType()
    {
        var service = new WebhookSubscriptionBootstrapService(new StubWebhookSubscriptionStore());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertAsync(
            new WebhookSubscriptionUpsertRequest
            {
                TenantId = Guid.NewGuid(),
                ApplicationClientId = Guid.NewGuid(),
                EndpointUrl = new Uri("https://crm.example.com/webhooks/otpauth"),
                EventTypes = ["challenge.failed"],
            },
            CancellationToken.None));

        Assert.Equal("Unsupported webhook event types: challenge.failed.", exception.Message);
    }

    private sealed class StubWebhookSubscriptionStore : IWebhookSubscriptionStore
    {
        public Task<IReadOnlyCollection<WebhookSubscription>> ListAsync(
            WebhookSubscriptionListRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<WebhookSubscription>>(Array.Empty<WebhookSubscription>());
        }

        public Task<WebhookSubscription> UpsertAsync(
            WebhookSubscriptionUpsertRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new WebhookSubscription
            {
                SubscriptionId = Guid.NewGuid(),
                TenantId = request.TenantId,
                ApplicationClientId = request.ApplicationClientId,
                EndpointUrl = request.EndpointUrl,
                IsActive = true,
                EventTypes = request.EventTypes.ToArray(),
                CreatedUtc = DateTimeOffset.UtcNow,
            });
        }
    }
}
