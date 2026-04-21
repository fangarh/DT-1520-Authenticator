using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Admin;

public static class AdminDeliveryStatusRequestMapper
{
    public static bool TryMap(
        Guid tenantId,
        Guid? applicationClientId,
        string? channel,
        string? status,
        int? limit,
        out AdminDeliveryStatusListRequest? request,
        out string? validationError)
    {
        if (!TryMapChannel(channel, out var mappedChannel, out validationError) ||
            !TryMapStatus(status, out var mappedStatus, out validationError))
        {
            request = null;
            return false;
        }

        request = new AdminDeliveryStatusListRequest
        {
            TenantId = tenantId,
            ApplicationClientId = applicationClientId,
            Channel = mappedChannel,
            Status = mappedStatus,
            Limit = limit ?? 50,
        };
        validationError = null;
        return true;
    }

    public static AdminDeliveryStatusHttpResponse MapResponse(AdminDeliveryStatusView delivery)
    {
        return new AdminDeliveryStatusHttpResponse
        {
            DeliveryId = delivery.DeliveryId,
            TenantId = delivery.TenantId,
            ApplicationClientId = delivery.ApplicationClientId,
            Channel = ToHttpValue(delivery.Channel),
            Status = ToHttpValue(delivery.Status),
            EventType = delivery.EventType,
            DeliveryDestination = SanitizeDestination(delivery.DeliveryDestination),
            SubjectType = delivery.SubjectType,
            SubjectId = delivery.SubjectId,
            PublicationId = delivery.PublicationId,
            AttemptCount = delivery.AttemptCount,
            OccurredAtUtc = delivery.OccurredAtUtc,
            CreatedAtUtc = delivery.CreatedAtUtc,
            NextAttemptAtUtc = delivery.NextAttemptAtUtc,
            LastAttemptAtUtc = delivery.LastAttemptAtUtc,
            DeliveredAtUtc = delivery.DeliveredAtUtc,
            LastErrorCode = delivery.LastErrorCode,
            IsRetryScheduled = delivery.IsRetryScheduled,
        };
    }

    private static bool TryMapChannel(
        string? channel,
        out AdminDeliveryChannel? mappedChannel,
        out string? validationError)
    {
        switch (channel?.Trim())
        {
            case null:
            case "":
                mappedChannel = null;
                validationError = null;
                return true;
            case "challenge_callback":
                mappedChannel = AdminDeliveryChannel.ChallengeCallback;
                validationError = null;
                return true;
            case "webhook_event":
                mappedChannel = AdminDeliveryChannel.WebhookEvent;
                validationError = null;
                return true;
            default:
                mappedChannel = null;
                validationError = "Channel must be one of: challenge_callback, webhook_event.";
                return false;
        }
    }

    private static bool TryMapStatus(
        string? status,
        out AdminDeliveryStatus? mappedStatus,
        out string? validationError)
    {
        switch (status?.Trim())
        {
            case null:
            case "":
                mappedStatus = null;
                validationError = null;
                return true;
            case "queued":
                mappedStatus = AdminDeliveryStatus.Queued;
                validationError = null;
                return true;
            case "delivered":
                mappedStatus = AdminDeliveryStatus.Delivered;
                validationError = null;
                return true;
            case "failed":
                mappedStatus = AdminDeliveryStatus.Failed;
                validationError = null;
                return true;
            default:
                mappedStatus = null;
                validationError = "Status must be one of: queued, delivered, failed.";
                return false;
        }
    }

    private static string ToHttpValue(AdminDeliveryChannel channel)
    {
        return channel switch
        {
            AdminDeliveryChannel.ChallengeCallback => "challenge_callback",
            AdminDeliveryChannel.WebhookEvent => "webhook_event",
            _ => throw new InvalidOperationException($"Unsupported admin delivery channel '{channel}'."),
        };
    }

    private static string ToHttpValue(AdminDeliveryStatus status)
    {
        return status switch
        {
            AdminDeliveryStatus.Queued => "queued",
            AdminDeliveryStatus.Delivered => "delivered",
            AdminDeliveryStatus.Failed => "failed",
            _ => throw new InvalidOperationException($"Unsupported admin delivery status '{status}'."),
        };
    }

    private static string SanitizeDestination(string rawDestination)
    {
        if (!Uri.TryCreate(rawDestination, UriKind.Absolute, out var uri))
        {
            return rawDestination;
        }

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
