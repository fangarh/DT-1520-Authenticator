using System.Security.Cryptography;
using OtpAuth.Application.Enrollments;

namespace OtpAuth.Application.Administration;

public sealed class AdminReplaceTotpEnrollmentHandler
{
    private const int TotpDigits = 6;
    private const int TotpPeriodSeconds = 30;
    private const int TotpSecretBytes = 20;
    private const string TotpAlgorithm = "SHA1";
    private const string DefaultIssuer = "OTPAuth";

    private readonly ITotpEnrollmentProvisioningStore _provisioningStore;
    private readonly ITotpEnrollmentAuditWriter _auditWriter;
    private readonly IAdminTotpEnrollmentAuditWriter _adminAuditWriter;

    public AdminReplaceTotpEnrollmentHandler(
        ITotpEnrollmentProvisioningStore provisioningStore,
        ITotpEnrollmentAuditWriter auditWriter,
        IAdminTotpEnrollmentAuditWriter adminAuditWriter)
    {
        _provisioningStore = provisioningStore;
        _auditWriter = auditWriter;
        _adminAuditWriter = adminAuditWriter;
    }

    public async Task<ReplaceTotpEnrollmentResult> HandleAsync(
        Guid enrollmentId,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        var accessError = ValidateAccess(adminContext);
        if (accessError is not null)
        {
            return ReplaceTotpEnrollmentResult.Failure(ReplaceTotpEnrollmentErrorCode.AccessDenied, accessError);
        }

        var enrollment = await _provisioningStore.GetByIdForAdminAsync(enrollmentId, cancellationToken);
        if (enrollment is null)
        {
            return ReplaceTotpEnrollmentResult.Failure(
                ReplaceTotpEnrollmentErrorCode.NotFound,
                $"Enrollment '{enrollmentId}' was not found.");
        }

        if (!enrollment.IsActive)
        {
            return ReplaceTotpEnrollmentResult.Failure(
                ReplaceTotpEnrollmentErrorCode.Conflict,
                $"Enrollment '{enrollmentId}' is revoked and cannot be replaced.");
        }

        if (!enrollment.ConfirmedUtc.HasValue)
        {
            return ReplaceTotpEnrollmentResult.Failure(
                ReplaceTotpEnrollmentErrorCode.Conflict,
                $"Enrollment '{enrollmentId}' is not confirmed and cannot be replaced.");
        }

        var secret = RandomNumberGenerator.GetBytes(TotpSecretBytes);
        var replacement = await _provisioningStore.UpsertPendingReplacementAsync(
            new TotpEnrollmentReplacementDraft
            {
                EnrollmentId = enrollment.EnrollmentId,
                Secret = secret,
                Digits = TotpDigits,
                PeriodSeconds = TotpPeriodSeconds,
                Algorithm = TotpAlgorithm,
            },
            cancellationToken);

        var issuer = DefaultIssuer;
        var label = enrollment.Label ?? enrollment.ExternalUserId;
        var secretUri = TotpProvisioningUriBuilder.Build(
            issuer,
            label,
            replacement.PendingReplacement!.Secret,
            replacement.PendingReplacement.Digits,
            replacement.PendingReplacement.PeriodSeconds,
            replacement.PendingReplacement.Algorithm);
        var response = new TotpEnrollmentView
        {
            EnrollmentId = replacement.EnrollmentId,
            Status = TotpEnrollmentStatus.Confirmed,
            HasPendingReplacement = true,
            SecretUri = secretUri,
            QrCodePayload = secretUri,
        };

        await _auditWriter.WriteReplacementStartedAsync(
            response,
            replacement.TenantId,
            replacement.ApplicationClientId,
            replacement.ExternalUserId,
            label,
            issuer,
            cancellationToken);
        await _adminAuditWriter.WriteReplacementStartedAsync(
            adminContext,
            response,
            replacement.TenantId,
            replacement.ApplicationClientId,
            replacement.ExternalUserId,
            label,
            issuer,
            cancellationToken);

        return ReplaceTotpEnrollmentResult.Success(response);
    }

    private static string? ValidateAccess(AdminContext adminContext)
    {
        return adminContext.HasPermission(AdminPermissions.EnrollmentsWrite)
            ? null
            : $"Permission '{AdminPermissions.EnrollmentsWrite}' is required.";
    }
}
