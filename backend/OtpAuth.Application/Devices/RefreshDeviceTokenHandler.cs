using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public sealed class RefreshDeviceTokenHandler
{
    private const string InvalidTokenMessage = "Refresh token is invalid or expired.";

    private readonly IDeviceRegistryStore _deviceRegistryStore;
    private readonly IDeviceRefreshTokenHasher _deviceRefreshTokenHasher;
    private readonly IDeviceAccessTokenIssuer _deviceAccessTokenIssuer;
    private readonly IDeviceLifecycleAuditWriter _auditWriter;

    public RefreshDeviceTokenHandler(
        IDeviceRegistryStore deviceRegistryStore,
        IDeviceRefreshTokenHasher deviceRefreshTokenHasher,
        IDeviceAccessTokenIssuer deviceAccessTokenIssuer,
        IDeviceLifecycleAuditWriter auditWriter)
    {
        _deviceRegistryStore = deviceRegistryStore;
        _deviceRefreshTokenHasher = deviceRefreshTokenHasher;
        _deviceAccessTokenIssuer = deviceAccessTokenIssuer;
        _auditWriter = auditWriter;
    }

    public async Task<RefreshDeviceTokenResult> HandleAsync(
        RefreshDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return RefreshDeviceTokenResult.Failure(
                RefreshDeviceTokenErrorCode.ValidationFailed,
                "RefreshToken is required.");
        }

        if (!DeviceRefreshTokenFormat.TryParse(request.RefreshToken, out var tokenId, out var tokenSecret))
        {
            return RefreshDeviceTokenResult.Failure(RefreshDeviceTokenErrorCode.InvalidToken, InvalidTokenMessage);
        }

        var currentToken = await _deviceRegistryStore.GetRefreshTokenByIdAsync(tokenId, cancellationToken);
        if (currentToken is null || !_deviceRefreshTokenHasher.Verify(tokenSecret!, currentToken.TokenHash))
        {
            return RefreshDeviceTokenResult.Failure(RefreshDeviceTokenErrorCode.InvalidToken, InvalidTokenMessage);
        }

        var device = await _deviceRegistryStore.GetByIdAsync(currentToken.DeviceId, cancellationToken);
        if (device is null)
        {
            return RefreshDeviceTokenResult.Failure(RefreshDeviceTokenErrorCode.InvalidToken, InvalidTokenMessage);
        }

        if (ShouldBlockForReuse(currentToken, device))
        {
            var wasStateChanged = device.Status != DeviceStatus.Blocked;
            var blockedDevice = device.MarkBlocked(DateTimeOffset.UtcNow);
            await _deviceRegistryStore.BlockDeviceAsync(blockedDevice, blockedDevice.LastAuthStateChangedUtc, cancellationToken);
            await _auditWriter.WriteRefreshReuseDetectedAsync(blockedDevice, DescribeTokenState(currentToken, blockedDevice), cancellationToken);
            await _auditWriter.WriteBlockedAsync(blockedDevice, "refresh_token_reuse", wasStateChanged, cancellationToken);

            return RefreshDeviceTokenResult.Failure(RefreshDeviceTokenErrorCode.Conflict, InvalidTokenMessage);
        }

        var rotatedAtUtc = DateTimeOffset.UtcNow;
        var refreshedDevice = device.MarkSeen(rotatedAtUtc);
        var tokenMaterial = await _deviceAccessTokenIssuer.IssueAsync(refreshedDevice, currentToken.TokenFamilyId, cancellationToken);
        var replacementToken = new DeviceRefreshTokenRecord
        {
            TokenId = tokenMaterial.RefreshTokenId,
            DeviceId = refreshedDevice.Id,
            TokenFamilyId = currentToken.TokenFamilyId,
            TokenHash = _deviceRefreshTokenHasher.Hash(tokenMaterial.RefreshTokenSecret),
            IssuedUtc = rotatedAtUtc,
            ExpiresUtc = tokenMaterial.RefreshTokenExpiresUtc,
            CreatedUtc = rotatedAtUtc,
        };

        var rotated = await _deviceRegistryStore.RotateRefreshTokenAsync(
            new DeviceRefreshRotation
            {
                CurrentTokenId = currentToken.TokenId,
                NewToken = replacementToken,
                RotatedAtUtc = rotatedAtUtc,
            },
            refreshedDevice.Id,
            rotatedAtUtc,
            cancellationToken);
        if (!rotated)
        {
            var wasStateChanged = device.Status != DeviceStatus.Blocked;
            var blockedDevice = device.MarkBlocked(DateTimeOffset.UtcNow);
            await _deviceRegistryStore.BlockDeviceAsync(blockedDevice, blockedDevice.LastAuthStateChangedUtc, cancellationToken);
            await _auditWriter.WriteRefreshReuseDetectedAsync(blockedDevice, "rotation_race", cancellationToken);
            await _auditWriter.WriteBlockedAsync(blockedDevice, "refresh_rotation_race", wasStateChanged, cancellationToken);

            return RefreshDeviceTokenResult.Failure(RefreshDeviceTokenErrorCode.Conflict, InvalidTokenMessage);
        }

        await _auditWriter.WriteTokenRefreshedAsync(refreshedDevice, cancellationToken);
        return RefreshDeviceTokenResult.Success(tokenMaterial.TokenPair);
    }

    private static bool ShouldBlockForReuse(DeviceRefreshTokenRecord token, RegisteredDevice device)
    {
        if (device.Status != DeviceStatus.Active)
        {
            return true;
        }

        if (token.ConsumedUtc.HasValue || token.RevokedUtc.HasValue)
        {
            return true;
        }

        return token.ExpiresUtc <= DateTimeOffset.UtcNow;
    }

    private static string DescribeTokenState(DeviceRefreshTokenRecord token, RegisteredDevice device)
    {
        if (device.Status != DeviceStatus.Active)
        {
            return $"device_{device.Status.ToString().ToLowerInvariant()}";
        }

        if (token.RevokedUtc.HasValue)
        {
            return "revoked";
        }

        if (token.ConsumedUtc.HasValue)
        {
            return "consumed";
        }

        return token.ExpiresUtc <= DateTimeOffset.UtcNow
            ? "expired"
            : "rotation_race";
    }
}
