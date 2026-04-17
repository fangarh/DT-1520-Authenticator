using System.Security.Claims;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public interface IDeviceRefreshTokenHasher
{
    string Hash(string tokenSecret);

    bool Verify(string tokenSecret, string tokenHash);
}

public interface IDeviceAccessTokenIssuer
{
    Task<DeviceTokenMaterial> IssueAsync(RegisteredDevice device, Guid tokenFamilyId, CancellationToken cancellationToken);
}

public interface IDeviceAccessTokenRuntimeValidator
{
    Task<DeviceAccessTokenRuntimeValidationResult> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

public interface IDeviceLifecycleAuditWriter
{
    Task WriteActivatedAsync(RegisteredDevice device, CancellationToken cancellationToken);

    Task WriteTokenRefreshedAsync(RegisteredDevice device, CancellationToken cancellationToken);

    Task WriteRefreshReuseDetectedAsync(RegisteredDevice device, string tokenState, CancellationToken cancellationToken);

    Task WriteRevokedAsync(RegisteredDevice device, bool stateChanged, CancellationToken cancellationToken);

    Task WriteBlockedAsync(RegisteredDevice device, string reason, bool stateChanged, CancellationToken cancellationToken);
}
