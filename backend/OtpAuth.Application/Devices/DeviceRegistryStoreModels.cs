using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public sealed record DeviceRefreshRotation
{
    public required Guid CurrentTokenId { get; init; }

    public required DeviceRefreshTokenRecord NewToken { get; init; }

    public required DateTimeOffset RotatedAtUtc { get; init; }

    public Guid? ReplacedByTokenId => NewToken.TokenId;
}

public interface IDeviceRegistryStore
{
    Task<DeviceActivationCodeArtifact?> GetActivationCodeByIdAsync(Guid activationCodeId, CancellationToken cancellationToken);

    Task<RegisteredDevice?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken);

    Task<RegisteredDevice?> GetByIdAsync(Guid deviceId, Guid tenantId, Guid applicationClientId, CancellationToken cancellationToken);

    Task<RegisteredDevice?> GetActiveByInstallationAsync(Guid tenantId, Guid applicationClientId, string installationId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RegisteredDevice>> ListActiveByExternalUserAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken);

    Task<DeviceRefreshTokenRecord?> GetRefreshTokenByIdAsync(Guid tokenId, CancellationToken cancellationToken);

    Task<bool> ActivateAsync(
        RegisteredDevice device,
        DeviceRefreshTokenRecord refreshToken,
        Guid activationCodeId,
        DateTimeOffset activatedAtUtc,
        CancellationToken cancellationToken);

    Task<bool> RotateRefreshTokenAsync(
        DeviceRefreshRotation rotation,
        Guid deviceId,
        DateTimeOffset lastSeenUtc,
        CancellationToken cancellationToken);

    Task<bool> RevokeDeviceAsync(RegisteredDevice device, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken);

    Task<bool> BlockDeviceAsync(RegisteredDevice device, DateTimeOffset blockedAtUtc, CancellationToken cancellationToken);
}
