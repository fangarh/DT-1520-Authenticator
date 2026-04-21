using OtpAuth.Application.Enrollments;

namespace OtpAuth.Application.Administration;

public sealed class AdminRevokeTotpEnrollmentHandler
{
    private readonly ITotpEnrollmentProvisioningStore _provisioningStore;
    private readonly ITotpEnrollmentAuditWriter _auditWriter;
    private readonly IAdminTotpEnrollmentAuditWriter _adminAuditWriter;

    public AdminRevokeTotpEnrollmentHandler(
        ITotpEnrollmentProvisioningStore provisioningStore,
        ITotpEnrollmentAuditWriter auditWriter,
        IAdminTotpEnrollmentAuditWriter adminAuditWriter)
    {
        _provisioningStore = provisioningStore;
        _auditWriter = auditWriter;
        _adminAuditWriter = adminAuditWriter;
    }

    public async Task<RevokeTotpEnrollmentResult> HandleAsync(
        Guid enrollmentId,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateAccess(adminContext);
        if (accessError is not null)
        {
            return RevokeTotpEnrollmentResult.Failure(RevokeTotpEnrollmentErrorCode.AccessDenied, accessError);
        }

        var enrollment = await _provisioningStore.GetByIdForAdminAsync(enrollmentId, cancellationToken);
        if (enrollment is null)
        {
            return RevokeTotpEnrollmentResult.Failure(
                RevokeTotpEnrollmentErrorCode.NotFound,
                $"Enrollment '{enrollmentId}' was not found.");
        }

        if (!enrollment.IsActive)
        {
            return RevokeTotpEnrollmentResult.Failure(
                RevokeTotpEnrollmentErrorCode.Conflict,
                $"Enrollment '{enrollmentId}' is already revoked.");
        }

        var revokedAtUtc = DateTimeOffset.UtcNow;
        var revoked = await _provisioningStore.RevokeAsync(
            enrollment.EnrollmentId,
            revokedAtUtc,
            FactorRevocationSideEffects.CreateForTotp(enrollment, revokedAtUtc),
            cancellationToken);
        if (!revoked)
        {
            return RevokeTotpEnrollmentResult.Failure(
                RevokeTotpEnrollmentErrorCode.Conflict,
                $"Enrollment '{enrollmentId}' is already revoked.");
        }

        var response = new TotpEnrollmentView
        {
            EnrollmentId = enrollment.EnrollmentId,
            Status = TotpEnrollmentStatus.Revoked,
            HasPendingReplacement = false,
            RevokedAtUtc = revokedAtUtc,
        };

        await _auditWriter.WriteRevokedAsync(
            response,
            enrollment.TenantId,
            enrollment.ApplicationClientId,
            enrollment.ExternalUserId,
            cancellationToken);
        await _adminAuditWriter.WriteRevokedAsync(
            adminContext,
            response,
            enrollment.TenantId,
            enrollment.ApplicationClientId,
            enrollment.ExternalUserId,
            cancellationToken);

        return RevokeTotpEnrollmentResult.Success(response);
    }

    private static string? ValidateAccess(AdminContext adminContext)
    {
        return adminContext.HasPermission(AdminPermissions.EnrollmentsWrite)
            ? null
            : $"Permission '{AdminPermissions.EnrollmentsWrite}' is required.";
    }
}
