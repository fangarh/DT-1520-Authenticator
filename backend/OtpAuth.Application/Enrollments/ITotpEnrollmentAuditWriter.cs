namespace OtpAuth.Application.Enrollments;

public interface ITotpEnrollmentAuditWriter
{
    Task WriteStartedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        string? label,
        string issuer,
        CancellationToken cancellationToken);

    Task WriteConfirmedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken);

    Task WriteRevokedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken);

    Task WriteReplacementStartedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        string? label,
        string issuer,
        CancellationToken cancellationToken);

    Task WriteReplacementConfirmedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken);

    Task WriteReplacementConfirmationFailedAsync(
        Guid enrollmentId,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        int failedAttempts,
        bool attemptLimitReached,
        CancellationToken cancellationToken);

    Task WriteConfirmationFailedAsync(
        Guid enrollmentId,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        int failedAttempts,
        bool attemptLimitReached,
        CancellationToken cancellationToken);
}
