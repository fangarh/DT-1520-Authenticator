namespace OtpAuth.Infrastructure.Challenges;

public sealed class ChallengeCallbackDeliveryGatewayOptions
{
    public string? SigningKey { get; init; }

    public int TimeoutSeconds { get; init; } = 5;

    public string UserAgent { get; init; } = "OtpAuth-Callbacks/1.0";

    public byte[] GetSigningKeyBytes()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException("ChallengeCallbacks:SigningKey must be configured.");
        }

        return System.Text.Encoding.UTF8.GetBytes(SigningKey.Trim());
    }

    public TimeSpan GetTimeout()
    {
        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("ChallengeCallbacks:TimeoutSeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(TimeoutSeconds);
    }
}
