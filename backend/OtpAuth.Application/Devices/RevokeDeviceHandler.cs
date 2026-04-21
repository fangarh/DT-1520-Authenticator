using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public sealed class RevokeDeviceHandler
{
    private readonly IDeviceRegistryStore _deviceRegistryStore;
    private readonly IDeviceLifecycleAuditWriter _auditWriter;

    public RevokeDeviceHandler(
        IDeviceRegistryStore deviceRegistryStore,
        IDeviceLifecycleAuditWriter auditWriter)
    {
        _deviceRegistryStore = deviceRegistryStore;
        _auditWriter = auditWriter;
    }

    public async Task<RevokeDeviceResult> HandleAsync(
        Guid deviceId,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (deviceId == Guid.Empty)
        {
            return RevokeDeviceResult.Failure(RevokeDeviceErrorCode.ValidationFailed, "DeviceId is required.");
        }

        if (!clientContext.HasScope(IntegrationClientScopes.DevicesWrite))
        {
            return RevokeDeviceResult.Failure(
                RevokeDeviceErrorCode.AccessDenied,
                $"Scope '{IntegrationClientScopes.DevicesWrite}' is required.");
        }

        var device = await _deviceRegistryStore.GetByIdAsync(
            deviceId,
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            cancellationToken);
        if (device is null)
        {
            return RevokeDeviceResult.Failure(
                RevokeDeviceErrorCode.NotFound,
                $"Device '{deviceId}' was not found.");
        }

        var revokedDevice = device.MarkRevoked(DateTimeOffset.UtcNow);
        var wasStateChanged = revokedDevice.Status != device.Status || revokedDevice.RevokedUtc != device.RevokedUtc;

        if (wasStateChanged)
        {
            var revoked = await _deviceRegistryStore.RevokeDeviceAsync(
                revokedDevice,
                revokedDevice.LastAuthStateChangedUtc,
                DeviceLifecycleSideEffects.CreateFor(revokedDevice, revokedDevice.LastAuthStateChangedUtc),
                cancellationToken);
            if (!revoked)
            {
                return RevokeDeviceResult.Failure(
                    RevokeDeviceErrorCode.ValidationFailed,
                    "Device revoke could not be completed.");
            }
        }

        await _auditWriter.WriteRevokedAsync(revokedDevice, wasStateChanged, cancellationToken);
        return RevokeDeviceResult.Success(DeviceView.FromDevice(revokedDevice), wasStateChanged);
    }
}
