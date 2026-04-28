namespace OtpAuth.Application.Administration;

public sealed class AdminRevokeDeviceOnboardingArtifactHandler
{
    private readonly IAdminDeviceOnboardingStore _store;
    private readonly IAdminDeviceOnboardingAuditWriter _auditWriter;

    public AdminRevokeDeviceOnboardingArtifactHandler(
        IAdminDeviceOnboardingStore store,
        IAdminDeviceOnboardingAuditWriter auditWriter)
    {
        _store = store;
        _auditWriter = auditWriter;
    }

    public async Task<AdminRevokeDeviceOnboardingArtifactResult> HandleAsync(
        AdminDeviceOnboardingRouteRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.DevicesWrite))
        {
            return AdminRevokeDeviceOnboardingArtifactResult.Failure(
                AdminRevokeDeviceOnboardingArtifactErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.DevicesWrite}' is required.");
        }

        if (request.TenantId == Guid.Empty)
        {
            return AdminRevokeDeviceOnboardingArtifactResult.Failure(
                AdminRevokeDeviceOnboardingArtifactErrorCode.ValidationFailed,
                "TenantId is required.");
        }

        if (request.ActivationCodeId == Guid.Empty)
        {
            return AdminRevokeDeviceOnboardingArtifactResult.Failure(
                AdminRevokeDeviceOnboardingArtifactErrorCode.ValidationFailed,
                "ActivationCodeId is required.");
        }

        var revokeResult = await _store.RevokeAsync(
            request.TenantId,
            request.ActivationCodeId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!revokeResult.IsFound)
        {
            return AdminRevokeDeviceOnboardingArtifactResult.Failure(
                AdminRevokeDeviceOnboardingArtifactErrorCode.NotFound,
                "Device onboarding artifact was not found.");
        }

        if (!revokeResult.WasRevoked || revokeResult.Artifact is null)
        {
            return AdminRevokeDeviceOnboardingArtifactResult.Failure(
                AdminRevokeDeviceOnboardingArtifactErrorCode.Conflict,
                "Only pending device onboarding artifacts can be revoked.");
        }

        await _auditWriter.WriteRevokedAsync(adminContext, revokeResult.Artifact, cancellationToken);
        return AdminRevokeDeviceOnboardingArtifactResult.Success(revokeResult.Artifact);
    }
}
