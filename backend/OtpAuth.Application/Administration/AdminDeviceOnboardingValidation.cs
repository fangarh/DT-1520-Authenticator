using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Administration;

public static class AdminDeviceOnboardingValidation
{
    public const int DefaultTtlMinutes = 10;
    public const int MaxTtlMinutes = 60;
    public const int MaxExternalUserIdLength = 256;

    public static string? NormalizeExternalUserId(string? externalUserId)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return null;
        }

        var normalized = externalUserId.Trim();
        return normalized.Length > MaxExternalUserIdLength ? null : normalized;
    }

    public static string? ValidatePlatform(DevicePlatform platform)
    {
        return platform is DevicePlatform.Android
            ? null
            : "Platform must be android for the first QR onboarding slice.";
    }

    public static string? ValidateTtlMinutes(int ttlMinutes)
    {
        return ttlMinutes is >= 1 and <= MaxTtlMinutes
            ? null
            : $"TtlMinutes must be between 1 and {MaxTtlMinutes}.";
    }
}
