using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Administration;

public static class AdminUserDeviceViewFactory
{
    public static AdminUserDeviceView Create(RegisteredDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new AdminUserDeviceView
        {
            DeviceId = device.Id,
            Platform = device.Platform,
            Status = device.Status switch
            {
                DeviceStatus.Active => AdminDeviceLifecycleStatus.Active,
                DeviceStatus.Revoked => AdminDeviceLifecycleStatus.Revoked,
                DeviceStatus.Blocked => AdminDeviceLifecycleStatus.Blocked,
                _ => throw new InvalidOperationException($"Unsupported admin device status '{device.Status}'."),
            },
            IsPushCapable = !string.IsNullOrWhiteSpace(device.PushToken),
            ActivatedUtc = device.ActivatedUtc,
            LastSeenUtc = device.LastSeenUtc,
            RevokedUtc = device.RevokedUtc,
            BlockedUtc = device.BlockedUtc,
        };
    }
}
