using System.Security.Claims;
using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Infrastructure.Devices;

public sealed class DeviceAccessTokenRuntimeValidator : IDeviceAccessTokenRuntimeValidator
{
    private readonly IDeviceRegistryStore _deviceRegistryStore;

    public DeviceAccessTokenRuntimeValidator(IDeviceRegistryStore deviceRegistryStore)
    {
        _deviceRegistryStore = deviceRegistryStore;
    }

    public async Task<DeviceAccessTokenRuntimeValidationResult> ValidateAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deviceIdValue = principal.FindFirst("device_id")?.Value;
        var tenantIdValue = principal.FindFirst("tenant_id")?.Value;
        var applicationClientIdValue = principal.FindFirst("application_client_id")?.Value;
        var issuedAtValue = principal.FindFirst("iat")?.Value;

        if (!Guid.TryParse(deviceIdValue, out var deviceId) ||
            !Guid.TryParse(tenantIdValue, out var tenantId) ||
            !Guid.TryParse(applicationClientIdValue, out var applicationClientId) ||
            !long.TryParse(issuedAtValue, out var issuedAtUnixTimeSeconds))
        {
            return DeviceAccessTokenRuntimeValidationResult.Failure("Device access token is missing required claims.");
        }

        var device = await _deviceRegistryStore.GetByIdAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return DeviceAccessTokenRuntimeValidationResult.Failure("Device is inactive or unknown.");
        }

        if (device.TenantId != tenantId || device.ApplicationClientId != applicationClientId)
        {
            return DeviceAccessTokenRuntimeValidationResult.Failure("Device access token claims do not match the active device.");
        }

        if (device.Status != DeviceStatus.Active)
        {
            return DeviceAccessTokenRuntimeValidationResult.Failure("Device is not active.");
        }

        var issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnixTimeSeconds);
        if (issuedAtUtc < device.LastAuthStateChangedUtc)
        {
            return DeviceAccessTokenRuntimeValidationResult.Failure("Device access token is no longer valid for the current device state.");
        }

        return DeviceAccessTokenRuntimeValidationResult.Success();
    }
}
