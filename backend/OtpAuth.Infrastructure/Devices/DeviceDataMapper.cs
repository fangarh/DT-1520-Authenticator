using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;
using Riok.Mapperly.Abstractions;

namespace OtpAuth.Infrastructure.Devices;

internal sealed record RegisteredDevicePersistenceModel
{
    public required Guid Id { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required string InstallationId { get; init; }

    public string? DeviceName { get; init; }

    public required DeviceStatus Status { get; init; }

    public required DeviceAttestationStatus AttestationStatus { get; init; }

    public string? PushToken { get; init; }

    public string? PublicKey { get; init; }

    public DateTimeOffset? ActivatedUtc { get; init; }

    public DateTimeOffset? LastSeenUtc { get; init; }

    public required DateTimeOffset LastAuthStateChangedUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public DateTimeOffset? BlockedUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

internal sealed record DeviceRefreshTokenPersistenceModel
{
    public required Guid TokenId { get; init; }

    public required Guid DeviceId { get; init; }

    public required Guid TokenFamilyId { get; init; }

    public required string TokenHash { get; init; }

    public required DateTimeOffset IssuedUtc { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }

    public DateTimeOffset? ConsumedUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public Guid? ReplacedByTokenId { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

internal sealed record DeviceActivationCodePersistenceModel
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

[Mapper]
internal static partial class DeviceDataMapper
{
    public static partial RegisteredDevicePersistenceModel ToPersistenceModel(RegisteredDevice source);

    public static partial RegisteredDevice ToDomainModel(RegisteredDevicePersistenceModel source);

    public static partial DeviceRefreshTokenPersistenceModel ToPersistenceModel(DeviceRefreshTokenRecord source);

    public static partial DeviceRefreshTokenRecord ToApplicationModel(DeviceRefreshTokenPersistenceModel source);

    public static partial DeviceActivationCodeArtifact ToApplicationModel(DeviceActivationCodePersistenceModel source);
}
