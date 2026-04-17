using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public sealed record ActivateDeviceRequest
{
    public required Guid TenantId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required string ActivationCode { get; init; }

    public required string InstallationId { get; init; }

    public string? DeviceName { get; init; }

    public string? PushToken { get; init; }

    public string? PublicKey { get; init; }
}
