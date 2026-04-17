namespace OtpAuth.Domain.Devices;

public enum DeviceStatus
{
    Unknown = 0,
    Pending = 1,
    Active = 2,
    Revoked = 3,
    Blocked = 4,
}
