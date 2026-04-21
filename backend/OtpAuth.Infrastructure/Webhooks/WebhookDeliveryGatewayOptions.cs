namespace OtpAuth.Infrastructure.Webhooks;

public sealed class WebhookDeliveryGatewayOptions
{
    public string? SigningKey { get; init; }

    public int TimeoutSeconds { get; init; } = 5;

    public string UserAgent { get; init; } = "OtpAuth-Webhooks/1.0";

    public TimeSpan GetTimeout()
    {
        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Webhooks:TimeoutSeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(TimeoutSeconds);
    }

    public byte[] GetSigningKeyBytes()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException("Webhooks:SigningKey must be configured.");
        }

        return System.Text.Encoding.UTF8.GetBytes(SigningKey);
    }
}
