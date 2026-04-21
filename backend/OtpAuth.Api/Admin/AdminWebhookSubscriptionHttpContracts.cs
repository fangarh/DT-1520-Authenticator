namespace OtpAuth.Api.Admin;

public sealed record AdminWebhookSubscriptionHttpResponse
{
    public required Guid SubscriptionId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string EndpointUrl { get; init; }

    public required string Status { get; init; }

    public IReadOnlyCollection<string> EventTypes { get; init; } = Array.Empty<string>();

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }
}

public sealed record AdminUpsertWebhookSubscriptionHttpRequest
{
    public required Guid TenantId { get; init; }

    public Guid? ApplicationClientId { get; init; }

    public required Uri EndpointUrl { get; init; }

    public IReadOnlyCollection<string> EventTypes { get; init; } = Array.Empty<string>();

    public bool IsActive { get; init; } = true;
}
