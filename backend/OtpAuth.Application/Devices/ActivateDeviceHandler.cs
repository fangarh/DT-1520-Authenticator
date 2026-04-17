using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Devices;

public sealed class ActivateDeviceHandler
{
    private readonly IDeviceRegistryStore _deviceRegistryStore;
    private readonly IDeviceRefreshTokenHasher _deviceRefreshTokenHasher;
    private readonly IDeviceAccessTokenIssuer _deviceAccessTokenIssuer;
    private readonly IDeviceLifecycleAuditWriter _auditWriter;

    public ActivateDeviceHandler(
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

    public async Task<ActivateDeviceResult> HandleAsync(
        ActivateDeviceRequest request,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationError = Validate(request, clientContext);
        if (validationError is not null)
        {
            return ActivateDeviceResult.Failure(ActivateDeviceErrorCode.ValidationFailed, validationError);
        }

        if (!clientContext.HasScope(IntegrationClientScopes.DevicesWrite))
        {
            return ActivateDeviceResult.Failure(
                ActivateDeviceErrorCode.AccessDenied,
                $"Scope '{IntegrationClientScopes.DevicesWrite}' is required.");
        }

        var existingDevice = await _deviceRegistryStore.GetActiveByInstallationAsync(
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            request.InstallationId.Trim(),
            cancellationToken);
        if (existingDevice is not null)
        {
            return ActivateDeviceResult.Failure(
                ActivateDeviceErrorCode.Conflict,
                "A device for this installation is already active.");
        }

        if (!DeviceActivationCodeFormat.TryParse(request.ActivationCode, out var activationCodeId, out var activationCodeSecret))
        {
            return ActivateDeviceResult.Failure(
                ActivateDeviceErrorCode.InvalidActivationCode,
                "Activation code is invalid or expired.");
        }

        var activationArtifact = await _deviceRegistryStore.GetActivationCodeByIdAsync(activationCodeId, cancellationToken);
        if (activationArtifact is null ||
            !_deviceRefreshTokenHasher.Verify(activationCodeSecret!, activationArtifact.CodeHash) ||
            activationArtifact.ConsumedUtc.HasValue ||
            activationArtifact.ExpiresUtc <= DateTimeOffset.UtcNow ||
            activationArtifact.TenantId != clientContext.TenantId ||
            activationArtifact.ApplicationClientId != clientContext.ApplicationClientId ||
            !string.Equals(activationArtifact.ExternalUserId, request.ExternalUserId.Trim(), StringComparison.Ordinal) ||
            activationArtifact.Platform != request.Platform)
        {
            return ActivateDeviceResult.Failure(
                ActivateDeviceErrorCode.InvalidActivationCode,
                "Activation code is invalid or expired.");
        }

        var activatedAtUtc = DateTimeOffset.UtcNow;
        var device = RegisteredDevice.Activate(
            Guid.NewGuid(),
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            request.ExternalUserId.Trim(),
            request.Platform,
            request.InstallationId.Trim(),
            NormalizeOptional(request.DeviceName),
            NormalizeOptional(request.PushToken),
            NormalizeOptional(request.PublicKey),
            activatedAtUtc);

        var tokenFamilyId = Guid.NewGuid();
        var tokenMaterial = await _deviceAccessTokenIssuer.IssueAsync(device, tokenFamilyId, cancellationToken);
        var refreshTokenRecord = new DeviceRefreshTokenRecord
        {
            TokenId = tokenMaterial.RefreshTokenId,
            DeviceId = device.Id,
            TokenFamilyId = tokenFamilyId,
            TokenHash = _deviceRefreshTokenHasher.Hash(tokenMaterial.RefreshTokenSecret),
            IssuedUtc = activatedAtUtc,
            ExpiresUtc = tokenMaterial.RefreshTokenExpiresUtc,
            CreatedUtc = activatedAtUtc,
        };

        var activated = await _deviceRegistryStore.ActivateAsync(
            device,
            refreshTokenRecord,
            activationCodeId,
            activatedAtUtc,
            cancellationToken);
        if (!activated)
        {
            return ActivateDeviceResult.Failure(
                ActivateDeviceErrorCode.Conflict,
                "Device activation could not be completed.");
        }

        await _auditWriter.WriteActivatedAsync(device, cancellationToken);
        return ActivateDeviceResult.Success(DeviceView.FromDevice(device), tokenMaterial.TokenPair);
    }

    private static string? Validate(ActivateDeviceRequest request, IntegrationClientContext clientContext)
    {
        if (request.TenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (request.TenantId != clientContext.TenantId)
        {
            return "TenantId does not match the authenticated integration client.";
        }

        if (string.IsNullOrWhiteSpace(request.ExternalUserId))
        {
            return "ExternalUserId is required.";
        }

        if (request.Platform is DevicePlatform.Unknown)
        {
            return "Platform is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ActivationCode))
        {
            return "ActivationCode is required.";
        }

        if (request.ActivationCode.Trim().Length > 256)
        {
            return "ActivationCode must be 256 characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(request.InstallationId))
        {
            return "InstallationId is required.";
        }

        if (request.InstallationId.Trim().Length is < 8 or > 128)
        {
            return "InstallationId must be between 8 and 128 characters.";
        }

        if (NormalizeOptional(request.DeviceName)?.Length > 128)
        {
            return "DeviceName must be 128 characters or fewer.";
        }

        if (NormalizeOptional(request.PushToken)?.Length > 2048)
        {
            return "PushToken must be 2048 characters or fewer.";
        }

        if (NormalizeOptional(request.PublicKey)?.Length > 4096)
        {
            return "PublicKey must be 4096 characters or fewer.";
        }

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
