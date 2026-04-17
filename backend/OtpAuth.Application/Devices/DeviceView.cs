using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public sealed record DeviceView
{
    public required Guid DeviceId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required DeviceStatus Status { get; init; }

    public required DeviceAttestationStatus AttestationStatus { get; init; }

    public string? DeviceName { get; init; }

    public required bool IsPushCapable { get; init; }

    public DateTimeOffset? ActivatedUtc { get; init; }

    public DateTimeOffset? LastSeenUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public DateTimeOffset? BlockedUtc { get; init; }

    public static DeviceView FromDevice(RegisteredDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceView
        {
            DeviceId = device.Id,
            Platform = device.Platform,
            Status = device.Status,
            AttestationStatus = device.AttestationStatus,
            DeviceName = device.DeviceName,
            IsPushCapable = !string.IsNullOrWhiteSpace(device.PushToken),
            ActivatedUtc = device.ActivatedUtc,
            LastSeenUtc = device.LastSeenUtc,
            RevokedUtc = device.RevokedUtc,
            BlockedUtc = device.BlockedUtc,
        };
    }
}
