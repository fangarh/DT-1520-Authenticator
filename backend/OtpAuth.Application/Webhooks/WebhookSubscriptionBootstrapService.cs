namespace OtpAuth.Application.Webhooks;

public sealed class WebhookSubscriptionBootstrapService
{
    private readonly IWebhookSubscriptionStore _store;

    public WebhookSubscriptionBootstrapService(IWebhookSubscriptionStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyCollection<WebhookSubscription>> ListAsync(
        WebhookSubscriptionListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _store.ListAsync(request, cancellationToken);
    }

    public async Task<WebhookSubscription> UpsertAsync(
        WebhookSubscriptionUpsertRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required.");
        }

        if (request.ApplicationClientId == Guid.Empty)
        {
            throw new InvalidOperationException("ApplicationClientId is required.");
        }

        var validationError = WebhookSubscriptionEndpointValidator.Validate(request.EndpointUrl);
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        var normalizedEventTypes = request.EventTypes
            .Where(static eventType => !string.IsNullOrWhiteSpace(eventType))
            .Select(static eventType => eventType.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static eventType => eventType, StringComparer.Ordinal)
            .ToArray();
        if (normalizedEventTypes.Length == 0)
        {
            throw new InvalidOperationException("At least one webhook event type is required.");
        }

        var unsupportedEventTypes = normalizedEventTypes
            .Where(eventType => !WebhookEventTypeNames.All.Contains(eventType, StringComparer.Ordinal))
            .ToArray();
        if (unsupportedEventTypes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Unsupported webhook event types: {string.Join(", ", unsupportedEventTypes)}.");
        }

        return await _store.UpsertAsync(
            request with
            {
                EventTypes = normalizedEventTypes,
            },
            cancellationToken);
    }
}
