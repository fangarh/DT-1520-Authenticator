using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public sealed record DeviceActivationCodeArtifact
{
    public required Guid ActivationCodeId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required string CodeHash { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }

    public DateTimeOffset? ConsumedUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}
