using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Enrollments;

public sealed class RevokeTotpEnrollmentHandler
{
    private readonly ITotpEnrollmentProvisioningStore _provisioningStore;
    private readonly ITotpEnrollmentAuditWriter _auditWriter;

    public RevokeTotpEnrollmentHandler(
        ITotpEnrollmentProvisioningStore provisioningStore,
        ITotpEnrollmentAuditWriter auditWriter)
    {
        _provisioningStore = provisioningStore;
        _auditWriter = auditWriter;
    }

    public async Task<RevokeTotpEnrollmentResult> HandleAsync(
        Guid enrollmentId,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateAccess(clientContext);
        if (accessError is not null)
        {
            return RevokeTotpEnrollmentResult.Failure(RevokeTotpEnrollmentErrorCode.AccessDenied, accessError);
        }

        var enrollment = await _provisioningStore.GetByIdAsync(
            enrollmentId,
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            cancellationToken);
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

        return RevokeTotpEnrollmentResult.Success(response);
    }

    private static string? ValidateAccess(IntegrationClientContext clientContext)
    {
        return clientContext.HasScope(IntegrationClientScopes.EnrollmentsWrite)
            ? null
            : $"Scope '{IntegrationClientScopes.EnrollmentsWrite}' is required.";
    }
}
