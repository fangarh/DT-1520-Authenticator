using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Devices;

public sealed class ListDevicesForRoutingHandler
{
    private readonly IDeviceRegistryStore _deviceRegistryStore;

    public ListDevicesForRoutingHandler(IDeviceRegistryStore deviceRegistryStore)
    {
        _deviceRegistryStore = deviceRegistryStore;
    }

    public async Task<ListDevicesForRoutingResult> HandleAsync(
        string externalUserId,
        bool pushCapableOnly,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        if (!clientContext.HasScope(IntegrationClientScopes.DevicesWrite))
        {
            return ListDevicesForRoutingResult.Failure(
                ListDevicesForRoutingErrorCode.AccessDenied,
                $"Scope '{IntegrationClientScopes.DevicesWrite}' is required.");
        }

        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return ListDevicesForRoutingResult.Failure(
                ListDevicesForRoutingErrorCode.ValidationFailed,
                "ExternalUserId is required.");
        }

        var activeDevices = await _deviceRegistryStore.ListActiveByExternalUserAsync(
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            externalUserId.Trim(),
            cancellationToken);
        var devices = activeDevices
            .Select(DeviceView.FromDevice)
            .Where(device => !pushCapableOnly || device.IsPushCapable)
            .OrderByDescending(device => device.IsPushCapable)
            .ThenByDescending(device => device.LastSeenUtc)
            .ThenBy(device => device.DeviceId)
            .ToArray();

        return ListDevicesForRoutingResult.Success(devices);
    }
}
