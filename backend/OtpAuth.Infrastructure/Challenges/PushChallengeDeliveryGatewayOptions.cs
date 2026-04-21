namespace OtpAuth.Infrastructure.Challenges;

public sealed class PushChallengeDeliveryGatewayOptions
{
    public string? Provider { get; init; }

    public FcmPushChallengeDeliveryGatewayOptions Fcm { get; init; } = new();

    public string GetProvider()
    {
        return string.IsNullOrWhiteSpace(Provider)
            ? PushChallengeDeliveryProviderNames.Logging
            : Provider.Trim().ToLowerInvariant();
    }

    public void Validate()
    {
        var provider = GetProvider();
        if (string.Equals(provider, PushChallengeDeliveryProviderNames.Logging, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(provider, PushChallengeDeliveryProviderNames.Fcm, StringComparison.Ordinal))
        {
            Fcm.Validate();
            return;
        }

        throw new InvalidOperationException(
            "PushDelivery:Provider must be one of 'logging' or 'fcm'.");
    }
}
