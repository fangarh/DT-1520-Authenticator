using System.Text;
using System.Text.Json;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class FcmPushChallengeDeliveryGatewayOptions
{
    public string? ProjectId { get; init; }

    public string? ServiceAccountJsonBase64 { get; init; }

    public string? ServiceAccountFilePath { get; init; }

    public int TimeoutSeconds { get; init; } = 5;

    public string UserAgent { get; init; } = "OtpAuth-Push-FCM/1.0";

    public string GetProjectId()
    {
        if (!string.IsNullOrWhiteSpace(ProjectId))
        {
            return ProjectId.Trim();
        }

        using var document = JsonDocument.Parse(GetServiceAccountJson());
        if (document.RootElement.TryGetProperty("project_id", out var projectIdElement))
        {
            var projectId = projectIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                return projectId.Trim();
            }
        }

        throw new InvalidOperationException(
            "PushDelivery:Fcm:ProjectId must be configured when the service account JSON does not contain project_id.");
    }

    public string GetServiceAccountJson()
    {
        var hasInlineBase64 = !string.IsNullOrWhiteSpace(ServiceAccountJsonBase64);
        var hasFilePath = !string.IsNullOrWhiteSpace(ServiceAccountFilePath);
        if (hasInlineBase64 == hasFilePath)
        {
            throw new InvalidOperationException(
                "PushDelivery:Fcm must configure exactly one of ServiceAccountJsonBase64 or ServiceAccountFilePath.");
        }

        return hasInlineBase64
            ? DecodeBase64Credentials()
            : File.ReadAllText(ServiceAccountFilePath!.Trim(), Encoding.UTF8);
    }

    public TimeSpan GetTimeout()
    {
        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "PushDelivery:Fcm:TimeoutSeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(TimeoutSeconds);
    }

    public string GetUserAgent()
    {
        if (string.IsNullOrWhiteSpace(UserAgent))
        {
            throw new InvalidOperationException("PushDelivery:Fcm:UserAgent must be configured.");
        }

        return UserAgent.Trim();
    }

    public void Validate()
    {
        GetTimeout();
        GetUserAgent();

        var credentialJson = GetServiceAccountJson();
        using var document = JsonDocument.Parse(credentialJson);

        if (!document.RootElement.TryGetProperty("type", out var typeElement) ||
            !string.Equals(typeElement.GetString(), "service_account", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PushDelivery:Fcm credentials must be a Google service_account JSON document.");
        }

        RequireProperty(document, "client_email");
        RequireProperty(document, "private_key");
        GetProjectId();
    }

    private string DecodeBase64Credentials()
    {
        try
        {
            var bytes = Convert.FromBase64String(ServiceAccountJsonBase64!.Trim());
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "PushDelivery:Fcm:ServiceAccountJsonBase64 must be valid Base64.",
                exception);
        }
    }

    private static void RequireProperty(JsonDocument document, string propertyName)
    {
        if (!document.RootElement.TryGetProperty(propertyName, out var propertyElement) ||
            string.IsNullOrWhiteSpace(propertyElement.GetString()))
        {
            throw new InvalidOperationException(
                $"PushDelivery:Fcm credentials must contain '{propertyName}'.");
        }
    }
}
