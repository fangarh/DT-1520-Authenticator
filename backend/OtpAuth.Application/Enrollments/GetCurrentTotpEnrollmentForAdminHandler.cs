using OtpAuth.Application.Administration;

namespace OtpAuth.Application.Enrollments;

public sealed class GetCurrentTotpEnrollmentForAdminHandler
{
    private readonly ITotpEnrollmentProvisioningStore _provisioningStore;

    public GetCurrentTotpEnrollmentForAdminHandler(ITotpEnrollmentProvisioningStore provisioningStore)
    {
        _provisioningStore = provisioningStore;
    }

    public async Task<GetCurrentTotpEnrollmentForAdminResult> HandleAsync(
        Guid tenantId,
        string externalUserId,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(tenantId, externalUserId);
        if (validationError is not null)
        {
            return GetCurrentTotpEnrollmentForAdminResult.Failure(
                GetCurrentTotpEnrollmentForAdminErrorCode.ValidationFailed,
                validationError);
        }

        if (!adminContext.HasPermission(AdminPermissions.EnrollmentsRead))
        {
            return GetCurrentTotpEnrollmentForAdminResult.Failure(
                GetCurrentTotpEnrollmentForAdminErrorCode.AccessDenied,
                $"Permission '{AdminPermissions.EnrollmentsRead}' is required.");
        }

        var normalizedExternalUserId = externalUserId.Trim();
        var enrollment = await _provisioningStore.GetCurrentByExternalUserIdAsync(
            tenantId,
            normalizedExternalUserId,
            cancellationToken);
        if (enrollment is null)
        {
            return GetCurrentTotpEnrollmentForAdminResult.Failure(
                GetCurrentTotpEnrollmentForAdminErrorCode.NotFound,
                $"Current TOTP enrollment for tenant '{tenantId}' and external user '{normalizedExternalUserId}' was not found.");
        }

        return GetCurrentTotpEnrollmentForAdminResult.Success(new TotpEnrollmentAdminView
        {
            EnrollmentId = enrollment.EnrollmentId,
            TenantId = enrollment.TenantId,
            ApplicationClientId = enrollment.ApplicationClientId,
            ExternalUserId = enrollment.ExternalUserId,
            Label = enrollment.Label,
            Status = enrollment.Status,
            HasPendingReplacement = enrollment.HasPendingReplacement,
            ConfirmedAtUtc = enrollment.ConfirmedUtc,
            RevokedAtUtc = enrollment.RevokedUtc,
        });
    }

    private static string? Validate(Guid tenantId, string externalUserId)
    {
        if (tenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return "ExternalUserId is required.";
        }

        return externalUserId.Trim().Length > 256
            ? "ExternalUserId must be 256 characters or fewer."
            : null;
    }
}
