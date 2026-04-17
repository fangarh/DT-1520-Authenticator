using OtpAuth.Application.Enrollments;

namespace OtpAuth.Application.Administration;

public interface IAdminTotpEnrollmentAuditWriter
{
    Task WriteStartedAsync(
        AdminContext adminContext,
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        string? label,
        string issuer,
        CancellationToken cancellationToken);

    Task WriteConfirmedAsync(
        AdminContext adminContext,
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        bool isReplacement,
        CancellationToken cancellationToken);

    Task WriteRevokedAsync(
        AdminContext adminContext,
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken);

    Task WriteReplacementStartedAsync(
        AdminContext adminContext,
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        string? label,
        string issuer,
        CancellationToken cancellationToken);

    Task WriteConfirmationFailedAsync(
        AdminContext adminContext,
        Guid enrollmentId,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        int failedAttempts,
        bool attemptLimitReached,
        bool isReplacement,
        CancellationToken cancellationToken);
}
