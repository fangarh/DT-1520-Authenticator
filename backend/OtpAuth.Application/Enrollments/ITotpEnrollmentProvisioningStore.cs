namespace OtpAuth.Application.Enrollments;

public interface ITotpEnrollmentProvisioningStore
{
    Task<TotpEnrollmentProvisioningRecord?> GetByIdAsync(
        Guid enrollmentId,
        Guid tenantId,
        Guid applicationClientId,
        CancellationToken cancellationToken);

    Task<TotpEnrollmentProvisioningRecord?> GetByIdForAdminAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken);

    Task<TotpEnrollmentProvisioningRecord?> GetByExternalUserIdAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken);

    Task<TotpEnrollmentProvisioningRecord?> GetCurrentByExternalUserIdAsync(
        Guid tenantId,
        string externalUserId,
        CancellationToken cancellationToken);

    Task<TotpEnrollmentProvisioningRecord> UpsertPendingAsync(
        TotpEnrollmentProvisioningDraft draft,
        CancellationToken cancellationToken);

    Task<TotpEnrollmentProvisioningRecord> UpsertPendingReplacementAsync(
        TotpEnrollmentReplacementDraft draft,
        CancellationToken cancellationToken);

    Task<bool> ConfirmAsync(
        Guid enrollmentId,
        DateTimeOffset confirmedAt,
        CancellationToken cancellationToken);

    Task<bool> ConfirmReplacementAsync(
        Guid enrollmentId,
        DateTimeOffset confirmedAt,
        CancellationToken cancellationToken);

    Task<bool> RevokeAsync(
        Guid enrollmentId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken);

    Task IncrementFailedConfirmationAttemptsAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken);

    Task IncrementFailedReplacementConfirmationAttemptsAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken);
}
