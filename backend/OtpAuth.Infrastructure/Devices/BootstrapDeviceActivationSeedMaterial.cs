using OtpAuth.Domain.Devices;

namespace OtpAuth.Infrastructure.Devices;

public sealed record BootstrapDeviceActivationSeedMaterial
{
    public required Guid ActivationCodeId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required string ActivationCodeHash { get; init; }

    public required string ActivationCode { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }
}
