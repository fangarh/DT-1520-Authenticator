using OtpAuth.Application.Webhooks;

namespace OtpAuth.Application.Administration;

public sealed record AdminWebhookSubscriptionUpsertRequest
{
    public required Guid TenantId { get; init; }

    public Guid? ApplicationClientId { get; init; }

    public required Uri EndpointUrl { get; init; }

    public IReadOnlyCollection<string> EventTypes { get; init; } = Array.Empty<string>();

    public bool IsActive { get; init; } = true;
}

public enum AdminListWebhookSubscriptionsErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
}

public sealed record AdminListWebhookSubscriptionsResult
{
    public bool IsSuccess { get; init; }

    public AdminListWebhookSubscriptionsErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyCollection<WebhookSubscription> Subscriptions { get; init; } = Array.Empty<WebhookSubscription>();

    public static AdminListWebhookSubscriptionsResult Success(IReadOnlyCollection<WebhookSubscription> subscriptions) => new()
    {
        IsSuccess = true,
        Subscriptions = subscriptions,
    };

    public static AdminListWebhookSubscriptionsResult Failure(
        AdminListWebhookSubscriptionsErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminUpsertWebhookSubscriptionErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
    Conflict = 3,
}

public sealed record AdminUpsertWebhookSubscriptionResult
{
    public bool IsSuccess { get; init; }

    public AdminUpsertWebhookSubscriptionErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public WebhookSubscription? Subscription { get; init; }

    public static AdminUpsertWebhookSubscriptionResult Success(WebhookSubscription subscription) => new()
    {
        IsSuccess = true,
        Subscription = subscription,
    };

    public static AdminUpsertWebhookSubscriptionResult Failure(
        AdminUpsertWebhookSubscriptionErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
