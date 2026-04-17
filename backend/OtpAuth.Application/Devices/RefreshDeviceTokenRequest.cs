namespace OtpAuth.Application.Devices;

public sealed record RefreshDeviceTokenRequest
{
    public required string RefreshToken { get; init; }
}
