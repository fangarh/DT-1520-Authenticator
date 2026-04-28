using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Devices;

public sealed class ActivateDeviceWithOnboardingPayloadHandler
{
    private readonly IDeviceRegistryStore _deviceRegistryStore;
    private readonly ActivateDeviceHandler _activateDeviceHandler;

    public ActivateDeviceWithOnboardingPayloadHandler(
        IDeviceRegistryStore deviceRegistryStore,
        ActivateDeviceHandler activateDeviceHandler)
    {
        _deviceRegistryStore = deviceRegistryStore;
        _activateDeviceHandler = activateDeviceHandler;
    }

    public async Task<ActivateDeviceResult> HandleAsync(
        ActivateDeviceWithOnboardingPayloadRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!DeviceActivationCodeFormat.TryParse(
                request.ActivationPayload,
                out var activationCodeId,
                out _))
        {
            return ActivateDeviceResult.Failure(
                ActivateDeviceErrorCode.InvalidActivationCode,
                "Activation code is invalid or expired.");
        }

        var artifact = await _deviceRegistryStore.GetActivationCodeByIdAsync(
            activationCodeId,
            cancellationToken);
        if (artifact is null)
        {
            return ActivateDeviceResult.Failure(
                ActivateDeviceErrorCode.InvalidActivationCode,
                "Activation code is invalid or expired.");
        }

        var activationRequest = new ActivateDeviceRequest
        {
            TenantId = artifact.TenantId,
            ExternalUserId = artifact.ExternalUserId,
            Platform = request.Platform,
            ActivationCode = request.ActivationPayload,
            InstallationId = request.InstallationId,
            DeviceName = request.DeviceName,
            PushToken = request.PushToken,
            PublicKey = request.PublicKey,
        };
        var clientContext = new IntegrationClientContext
        {
            ClientId = "device-onboarding-artifact",
            TenantId = artifact.TenantId,
            ApplicationClientId = artifact.ApplicationClientId,
            Scopes = [IntegrationClientScopes.DevicesWrite],
        };

        return await _activateDeviceHandler.HandleAsync(
            activationRequest,
            clientContext,
            cancellationToken);
    }
}
