namespace OtpAuth.Infrastructure.Devices;

public sealed class DeviceTokenOptions
{
    public string Issuer { get; init; } = "otpauth-device";

    public string Audience { get; init; } = "otpauth-device-api";

    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    public int RefreshTokenLifetimeDays { get; init; } = 30;
}
