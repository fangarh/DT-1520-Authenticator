using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

internal sealed record AdminDeliveryStatusPersistenceModel
{
    public required Guid DeliveryId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string Channel { get; init; }

    public required string Status { get; init; }

    public required string EventType { get; init; }

    public required string DestinationUrl { get; init; }

    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public Guid? PublicationId { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset NextAttemptAtUtc { get; init; }

    public DateTimeOffset? LastAttemptAtUtc { get; init; }

    public DateTimeOffset? DeliveredAtUtc { get; init; }

    public string? LastErrorCode { get; init; }
}

internal static class AdminDeliveryStatusDataMapper
{
    public static string? ToPersistenceValue(AdminDeliveryChannel? channel)
    {
        return channel switch
        {
            null => null,
            AdminDeliveryChannel.ChallengeCallback => "challenge_callback",
            AdminDeliveryChannel.WebhookEvent => "webhook_event",
            _ => throw new InvalidOperationException($"Unsupported admin delivery channel '{channel}'."),
        };
    }

    public static string? ToPersistenceValue(AdminDeliveryStatus? status)
    {
        return status switch
        {
            null => null,
            AdminDeliveryStatus.Queued => "queued",
            AdminDeliveryStatus.Delivered => "delivered",
            AdminDeliveryStatus.Failed => "failed",
            _ => throw new InvalidOperationException($"Unsupported admin delivery status '{status}'."),
        };
    }

    public static AdminDeliveryStatusView ToDomainModel(AdminDeliveryStatusPersistenceModel source)
    {
        return new AdminDeliveryStatusView
        {
            DeliveryId = source.DeliveryId,
            TenantId = source.TenantId,
            ApplicationClientId = source.ApplicationClientId,
            Channel = FromPersistenceChannel(source.Channel),
            Status = FromPersistenceStatus(source.Status),
            EventType = source.EventType,
            DeliveryDestination = SanitizeDestination(source.DestinationUrl),
            SubjectType = source.SubjectType,
            SubjectId = source.SubjectId,
            PublicationId = source.PublicationId,
            AttemptCount = source.AttemptCount,
            OccurredAtUtc = source.OccurredAtUtc,
            CreatedAtUtc = source.CreatedAtUtc,
            NextAttemptAtUtc = source.NextAttemptAtUtc,
            LastAttemptAtUtc = source.LastAttemptAtUtc,
            DeliveredAtUtc = source.DeliveredAtUtc,
            LastErrorCode = source.LastErrorCode,
        };
    }

    private static AdminDeliveryChannel FromPersistenceChannel(string channel)
    {
        return channel switch
        {
            "challenge_callback" => AdminDeliveryChannel.ChallengeCallback,
            "webhook_event" => AdminDeliveryChannel.WebhookEvent,
            _ => throw new InvalidOperationException($"Unsupported admin delivery channel '{channel}'."),
        };
    }

    private static AdminDeliveryStatus FromPersistenceStatus(string status)
    {
        return status switch
        {
            "queued" => AdminDeliveryStatus.Queued,
            "delivered" => AdminDeliveryStatus.Delivered,
            "failed" => AdminDeliveryStatus.Failed,
            _ => throw new InvalidOperationException($"Unsupported admin delivery status '{status}'."),
        };
    }

    private static string SanitizeDestination(string rawDestination)
    {
        var uri = new Uri(rawDestination, UriKind.Absolute);
        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };

        return builder.Uri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.UriEscaped);
    }
}
