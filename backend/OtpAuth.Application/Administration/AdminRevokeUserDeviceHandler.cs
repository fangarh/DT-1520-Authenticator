using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Administration;

public sealed class AdminRevokeUserDeviceHandler
{
    private readonly IDeviceRegistryStore _deviceRegistryStore;
    private readonly IDeviceLifecycleAuditWriter _auditWriter;
    private readonly IAdminDeviceAuditWriter _adminAuditWriter;

    public AdminRevokeUserDeviceHandler(
        IDeviceRegistryStore deviceRegistryStore,
        IDeviceLifecycleAuditWriter auditWriter,
        IAdminDeviceAuditWriter adminAuditWriter)
    {
        _deviceRegistryStore = deviceRegistryStore;
        _auditWriter = auditWriter;
        _adminAuditWriter = adminAuditWriter;
    }

    public async Task<AdminRevokeUserDeviceResult> HandleAsync(
        AdminRevokeUserDeviceRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        var normalizedExternalUserId = NormalizeExternalUserId(request.ExternalUserId, out var validationError);
        if (validationError is not null)
        {
            return AdminRevokeUserDeviceResult.Failure(
                AdminRevokeUserDeviceErrorCode.ValidationFailed,
                validationError);
        }

        if (request.TenantId == Guid.Empty)
        {
            return AdminRevokeUserDeviceResult.Failure(
                AdminRevokeUserDeviceErrorCode.ValidationFailed,
                "TenantId is required.");
        }

        if (request.DeviceId == Guid.Empty)
        {
            return AdminRevokeUserDeviceResult.Failure(
                AdminRevokeUserDeviceErrorCode.ValidationFailed,
                "DeviceId is required.");
        }

        if (!adminContext.HasPermission(AdminPermissions.DevicesWrite))
        {
            return AdminRevokeUserDeviceResult.Failure(
                AdminRevokeUserDeviceErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.DevicesWrite}' is required.");
        }

        var device = await _deviceRegistryStore.GetByIdAsync(request.DeviceId, cancellationToken);
        if (device is null ||
            device.TenantId != request.TenantId ||
            !string.Equals(device.ExternalUserId, normalizedExternalUserId, StringComparison.Ordinal))
        {
            return AdminRevokeUserDeviceResult.Failure(
                AdminRevokeUserDeviceErrorCode.NotFound,
                $"Device '{request.DeviceId}' was not found.");
        }

        if (device.Status != DeviceStatus.Active)
        {
            return device.Status switch
            {
                DeviceStatus.Revoked => AdminRevokeUserDeviceResult.Failure(
                    AdminRevokeUserDeviceErrorCode.Conflict,
                    $"Device '{request.DeviceId}' is already revoked."),
                DeviceStatus.Blocked => AdminRevokeUserDeviceResult.Failure(
                    AdminRevokeUserDeviceErrorCode.Conflict,
                    $"Device '{request.DeviceId}' is blocked and cannot be revoked again."),
                _ => AdminRevokeUserDeviceResult.Failure(
                    AdminRevokeUserDeviceErrorCode.NotFound,
                    $"Device '{request.DeviceId}' was not found."),
            };
        }

        var revokedAtUtc = DateTimeOffset.UtcNow;
        var revokedDevice = device.MarkRevoked(revokedAtUtc);
        var revoked = await _deviceRegistryStore.RevokeDeviceAsync(
            revokedDevice,
            revokedAtUtc,
            DeviceLifecycleSideEffects.CreateFor(revokedDevice, revokedAtUtc),
            cancellationToken);
        if (!revoked)
        {
            return AdminRevokeUserDeviceResult.Failure(
                AdminRevokeUserDeviceErrorCode.Conflict,
                "Device revoke could not be completed.");
        }

        await _auditWriter.WriteRevokedAsync(revokedDevice, stateChanged: true, cancellationToken);
        await _adminAuditWriter.WriteRevokedAsync(adminContext, revokedDevice, cancellationToken);
        return AdminRevokeUserDeviceResult.Success(AdminUserDeviceViewFactory.Create(revokedDevice));
    }

    private static string? NormalizeExternalUserId(string externalUserId, out string? validationError)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            validationError = "ExternalUserId is required.";
            return null;
        }

        var normalizedExternalUserId = externalUserId.Trim();
        validationError = normalizedExternalUserId.Length > 256
            ? "ExternalUserId must be 256 characters or fewer."
            : null;

        return validationError is null
            ? normalizedExternalUserId
            : null;
    }
}
