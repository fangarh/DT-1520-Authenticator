using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Administration;

public sealed class AdminCreateDeviceOnboardingArtifactHandler
{
    private readonly IAdminDeviceOnboardingStore _store;
    private readonly IDeviceRefreshTokenHasher _activationSecretHasher;
    private readonly IAdminDeviceActivationSecretGenerator _secretGenerator;
    private readonly IAdminDeviceOnboardingAuditWriter _auditWriter;

    public AdminCreateDeviceOnboardingArtifactHandler(
        IAdminDeviceOnboardingStore store,
        IDeviceRefreshTokenHasher activationSecretHasher,
        IAdminDeviceActivationSecretGenerator secretGenerator,
        IAdminDeviceOnboardingAuditWriter auditWriter)
    {
        _store = store;
        _activationSecretHasher = activationSecretHasher;
        _secretGenerator = secretGenerator;
        _auditWriter = auditWriter;
    }

    public async Task<AdminCreateDeviceOnboardingArtifactResult> HandleAsync(
        AdminDeviceOnboardingCreateRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.DevicesWrite))
        {
            return AdminCreateDeviceOnboardingArtifactResult.Failure(
                AdminCreateDeviceOnboardingArtifactErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.DevicesWrite}' is required.");
        }

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return AdminCreateDeviceOnboardingArtifactResult.Failure(
                AdminCreateDeviceOnboardingArtifactErrorCode.ValidationFailed,
                validationError);
        }

        var activationCodeId = Guid.NewGuid();
        var activationSecret = _secretGenerator.Generate();
        var activationPayload = DeviceActivationCodeFormat.Create(activationCodeId, activationSecret);
        var now = DateTimeOffset.UtcNow;
        var artifact = await _store.CreateAsync(
            new AdminDeviceOnboardingCreateDraft
            {
                ActivationCodeId = activationCodeId,
                TenantId = request.TenantId,
                ApplicationClientId = request.ApplicationClientId,
                ExternalUserId = request.ExternalUserId.Trim(),
                Platform = request.Platform,
                CodeHash = _activationSecretHasher.Hash(activationSecret),
                ExpiresUtc = now.AddMinutes(request.TtlMinutes),
                CreatedUtc = now,
            },
            cancellationToken);
        if (artifact is null)
        {
            return AdminCreateDeviceOnboardingArtifactResult.Failure(
                AdminCreateDeviceOnboardingArtifactErrorCode.Conflict,
                "Device onboarding artifact could not be created.");
        }

        await _auditWriter.WriteCreatedAsync(adminContext, artifact, cancellationToken);
        return AdminCreateDeviceOnboardingArtifactResult.Success(artifact, activationPayload);
    }

    private static string? Validate(AdminDeviceOnboardingCreateRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (request.ApplicationClientId == Guid.Empty)
        {
            return "ApplicationClientId is required.";
        }

        var externalUserId = AdminDeviceOnboardingValidation.NormalizeExternalUserId(request.ExternalUserId);
        if (externalUserId is null)
        {
            return "ExternalUserId is required and must be 256 characters or fewer.";
        }

        var platformError = AdminDeviceOnboardingValidation.ValidatePlatform(request.Platform);
        if (platformError is not null)
        {
            return platformError;
        }

        var ttlError = AdminDeviceOnboardingValidation.ValidateTtlMinutes(request.TtlMinutes);
        if (ttlError is not null)
        {
            return ttlError;
        }

        return request.HasOperatorProvidedActivationPayload
            ? "Device onboarding artifacts generate activation payloads server-side."
            : null;
    }
}
