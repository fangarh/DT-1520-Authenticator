namespace OtpAuth.Application.Administration;

public sealed class AdminListDeviceOnboardingArtifactsHandler
{
    private readonly IAdminDeviceOnboardingStore _store;

    public AdminListDeviceOnboardingArtifactsHandler(IAdminDeviceOnboardingStore store)
    {
        _store = store;
    }

    public async Task<AdminListDeviceOnboardingArtifactsResult> HandleAsync(
        AdminDeviceOnboardingListRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!adminContext.HasPermission(AdminPermissions.DevicesRead))
        {
            return AdminListDeviceOnboardingArtifactsResult.Failure(
                AdminListDeviceOnboardingArtifactsErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.DevicesRead}' is required.");
        }

        if (request.TenantId == Guid.Empty)
        {
            return AdminListDeviceOnboardingArtifactsResult.Failure(
                AdminListDeviceOnboardingArtifactsErrorCode.ValidationFailed,
                "TenantId is required.");
        }

        string? externalUserId = null;
        if (!string.IsNullOrWhiteSpace(request.ExternalUserId))
        {
            externalUserId = AdminDeviceOnboardingValidation.NormalizeExternalUserId(request.ExternalUserId);
            if (externalUserId is null)
            {
                return AdminListDeviceOnboardingArtifactsResult.Failure(
                    AdminListDeviceOnboardingArtifactsErrorCode.ValidationFailed,
                    "ExternalUserId must be 256 characters or fewer.");
            }
        }

        if (request.ApplicationClientId == Guid.Empty)
        {
            return AdminListDeviceOnboardingArtifactsResult.Failure(
                AdminListDeviceOnboardingArtifactsErrorCode.ValidationFailed,
                "ApplicationClientId must not be empty.");
        }

        if (request.Limit is < 1 or > 100)
        {
            return AdminListDeviceOnboardingArtifactsResult.Failure(
                AdminListDeviceOnboardingArtifactsErrorCode.ValidationFailed,
                "Limit must be between 1 and 100.");
        }

        var artifacts = await _store.ListAsync(
            request with
            {
                ExternalUserId = externalUserId,
            },
            DateTimeOffset.UtcNow,
            cancellationToken);
        return artifacts.Count == 0
            ? AdminListDeviceOnboardingArtifactsResult.Failure(
                AdminListDeviceOnboardingArtifactsErrorCode.NotFound,
                "Device onboarding artifacts were not found.")
            : AdminListDeviceOnboardingArtifactsResult.Success(artifacts);
    }
}
