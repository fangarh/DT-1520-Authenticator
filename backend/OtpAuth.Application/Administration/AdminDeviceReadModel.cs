using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Administration;

public enum AdminDeviceLifecycleStatus
{
    Active = 0,
    Revoked = 1,
    Blocked = 2,
}

public sealed record AdminUserDeviceListRequest
{
    public required Guid TenantId { get; init; }

    public required string ExternalUserId { get; init; }
}

public sealed record AdminUserDeviceView
{
    public required Guid DeviceId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required AdminDeviceLifecycleStatus Status { get; init; }

    public required bool IsPushCapable { get; init; }

    public DateTimeOffset? ActivatedUtc { get; init; }

    public DateTimeOffset? LastSeenUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public DateTimeOffset? BlockedUtc { get; init; }
}

public sealed record AdminRevokeUserDeviceRequest
{
    public required Guid TenantId { get; init; }

    public required string ExternalUserId { get; init; }

    public required Guid DeviceId { get; init; }
}

public enum AdminListUserDevicesErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
}

public sealed record AdminListUserDevicesResult
{
    public bool IsSuccess { get; init; }

    public AdminListUserDevicesErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyCollection<AdminUserDeviceView> Devices { get; init; } = Array.Empty<AdminUserDeviceView>();

    public static AdminListUserDevicesResult Success(IReadOnlyCollection<AdminUserDeviceView> devices) => new()
    {
        IsSuccess = true,
        Devices = devices,
    };

    public static AdminListUserDevicesResult Failure(
        AdminListUserDevicesErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminRevokeUserDeviceErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
    Conflict = 3,
}

public sealed record AdminRevokeUserDeviceResult
{
    public bool IsSuccess { get; init; }

    public AdminRevokeUserDeviceErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminUserDeviceView? Device { get; init; }

    public static AdminRevokeUserDeviceResult Success(AdminUserDeviceView device) => new()
    {
        IsSuccess = true,
        Device = device,
    };

    public static AdminRevokeUserDeviceResult Failure(
        AdminRevokeUserDeviceErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public interface IAdminDeviceStore
{
    Task<IReadOnlyCollection<AdminUserDeviceView>> ListByExternalUserAsync(
        AdminUserDeviceListRequest request,
        CancellationToken cancellationToken);
}
