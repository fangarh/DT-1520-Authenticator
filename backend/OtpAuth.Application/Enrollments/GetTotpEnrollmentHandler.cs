using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Enrollments;

public sealed class GetTotpEnrollmentHandler
{
    private readonly ITotpEnrollmentProvisioningStore _provisioningStore;

    public GetTotpEnrollmentHandler(ITotpEnrollmentProvisioningStore provisioningStore)
    {
        _provisioningStore = provisioningStore;
    }

    public async Task<GetTotpEnrollmentResult> HandleAsync(
        Guid enrollmentId,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateAccess(clientContext);
        if (accessError is not null)
        {
            return GetTotpEnrollmentResult.Failure(GetTotpEnrollmentErrorCode.AccessDenied, accessError);
        }

        var enrollment = await _provisioningStore.GetByIdAsync(
            enrollmentId,
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            cancellationToken);
        if (enrollment is null)
        {
            return GetTotpEnrollmentResult.Failure(
                GetTotpEnrollmentErrorCode.NotFound,
                $"Enrollment '{enrollmentId}' was not found.");
        }

        return GetTotpEnrollmentResult.Success(new TotpEnrollmentView
        {
            EnrollmentId = enrollment.EnrollmentId,
            Status = enrollment.Status,
            HasPendingReplacement = enrollment.HasPendingReplacement,
            ConfirmedAtUtc = enrollment.ConfirmedUtc,
            RevokedAtUtc = enrollment.RevokedUtc,
        });
    }

    private static string? ValidateAccess(IntegrationClientContext clientContext)
    {
        return clientContext.HasScope(IntegrationClientScopes.EnrollmentsWrite)
            ? null
            : $"Scope '{IntegrationClientScopes.EnrollmentsWrite}' is required.";
    }
}
