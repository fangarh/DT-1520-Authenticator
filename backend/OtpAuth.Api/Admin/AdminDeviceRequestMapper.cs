using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Api.Admin;

public static class AdminDeviceRequestMapper
{
    public static AdminUserDeviceHttpResponse MapResponse(AdminUserDeviceView device)
    {
        return new AdminUserDeviceHttpResponse
        {
            DeviceId = device.DeviceId,
            Platform = device.Platform switch
            {
                DevicePlatform.Android => "android",
                DevicePlatform.Ios => "ios",
                _ => "unknown",
            },
            Status = device.Status switch
            {
                AdminDeviceLifecycleStatus.Active => "active",
                AdminDeviceLifecycleStatus.Revoked => "revoked",
                AdminDeviceLifecycleStatus.Blocked => "blocked",
                _ => "unknown",
            },
            IsPushCapable = device.IsPushCapable,
            ActivatedAtUtc = device.ActivatedUtc,
            LastSeenAtUtc = device.LastSeenUtc,
            RevokedAtUtc = device.RevokedUtc,
            BlockedAtUtc = device.BlockedUtc,
        };
    }
}
