using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Infrastructure.Administration;

internal sealed record AdminDevicePersistenceModel
{
    public required Guid DeviceId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required DeviceStatus Status { get; init; }

    public required bool IsPushCapable { get; init; }

    public DateTimeOffset? ActivatedUtc { get; init; }

    public DateTimeOffset? LastSeenUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public DateTimeOffset? BlockedUtc { get; init; }
}

internal static class AdminDeviceDataMapper
{
    public static AdminUserDeviceView ToDomainModel(AdminDevicePersistenceModel source)
    {
        return new AdminUserDeviceView
        {
            DeviceId = source.DeviceId,
            Platform = source.Platform,
            Status = source.Status switch
            {
                DeviceStatus.Active => AdminDeviceLifecycleStatus.Active,
                DeviceStatus.Revoked => AdminDeviceLifecycleStatus.Revoked,
                DeviceStatus.Blocked => AdminDeviceLifecycleStatus.Blocked,
                _ => throw new InvalidOperationException($"Unsupported admin device status '{source.Status}'."),
            },
            IsPushCapable = source.IsPushCapable,
            ActivatedUtc = source.ActivatedUtc,
            LastSeenUtc = source.LastSeenUtc,
            RevokedUtc = source.RevokedUtc,
            BlockedUtc = source.BlockedUtc,
        };
    }
}
