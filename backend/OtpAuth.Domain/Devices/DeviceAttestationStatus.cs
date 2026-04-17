namespace OtpAuth.Domain.Devices;

public enum DeviceAttestationStatus
{
    Unknown = 0,
    NotProvided = 1,
    Pending = 2,
    Accepted = 3,
    Rejected = 4,
}
