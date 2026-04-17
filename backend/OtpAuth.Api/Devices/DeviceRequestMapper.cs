using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Api.Devices;

public static class DeviceRequestMapper
{
    public static bool TryMap(
        ActivateDeviceHttpRequest request,
        out ActivateDeviceRequest? applicationRequest,
        out string? validationError)
    {
        applicationRequest = null;
        validationError = null;

        var platform = request.Platform?.Trim().ToLowerInvariant() switch
        {
            "android" => DevicePlatform.Android,
            "ios" => DevicePlatform.Ios,
            _ => DevicePlatform.Unknown,
        };
        if (platform == DevicePlatform.Unknown)
        {
            validationError = "Platform must be 'android' or 'ios'.";
            return false;
        }

        applicationRequest = new ActivateDeviceRequest
        {
            TenantId = request.TenantId,
            ExternalUserId = request.ExternalUserId,
            Platform = platform,
            ActivationCode = request.ActivationCode,
            InstallationId = request.InstallationId,
            DeviceName = request.DeviceName,
            PushToken = request.PushToken,
            PublicKey = request.PublicKey,
        };

        return true;
    }

    public static RefreshDeviceTokenRequest Map(RefreshDeviceTokenHttpRequest request)
    {
        return new RefreshDeviceTokenRequest
        {
            RefreshToken = request.RefreshToken,
        };
    }

    public static DeviceActivationHttpResponse MapActivationResponse(DeviceView device, DeviceTokenPair tokens)
    {
        return new DeviceActivationHttpResponse
        {
            Device = MapDeviceResponse(device),
            Tokens = MapTokenResponse(tokens),
        };
    }

    public static DeviceHttpResponse MapDeviceResponse(DeviceView device)
    {
        return new DeviceHttpResponse
        {
            Id = device.DeviceId,
            Platform = ToContractValue(device.Platform),
            Status = ToContractValue(device.Status),
            AttestationStatus = ToContractValue(device.AttestationStatus),
            DeviceName = device.DeviceName,
            IsPushCapable = device.IsPushCapable,
            ActivatedAt = device.ActivatedUtc,
            LastSeenAt = device.LastSeenUtc,
            RevokedAt = device.RevokedUtc,
            BlockedAt = device.BlockedUtc,
        };
    }

    public static DeviceTokenHttpResponse MapTokenResponse(DeviceTokenPair tokenPair)
    {
        return new DeviceTokenHttpResponse
        {
            AccessToken = tokenPair.AccessToken,
            RefreshToken = tokenPair.RefreshToken,
            TokenType = tokenPair.TokenType,
            ExpiresIn = tokenPair.ExpiresIn,
            Scope = tokenPair.Scope,
        };
    }

    private static string ToContractValue(DevicePlatform platform)
    {
        return platform switch
        {
            DevicePlatform.Android => "android",
            DevicePlatform.Ios => "ios",
            _ => "unknown",
        };
    }

    private static string ToContractValue(DeviceStatus status)
    {
        return status switch
        {
            DeviceStatus.Pending => "pending",
            DeviceStatus.Active => "active",
            DeviceStatus.Revoked => "revoked",
            DeviceStatus.Blocked => "blocked",
            _ => "unknown",
        };
    }

    private static string ToContractValue(DeviceAttestationStatus status)
    {
        return status switch
        {
            DeviceAttestationStatus.NotProvided => "not_provided",
            DeviceAttestationStatus.Pending => "pending",
            DeviceAttestationStatus.Accepted => "accepted",
            DeviceAttestationStatus.Rejected => "rejected",
            _ => "unknown",
        };
    }
}
