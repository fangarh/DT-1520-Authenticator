using System.Text.Json;
using OtpAuth.Application.Enrollments;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Factors;

public sealed class TotpEnrollmentAuditWriter : ITotpEnrollmentAuditWriter
{
    private readonly SecurityAuditService _auditService;

    public TotpEnrollmentAuditWriter(SecurityAuditService auditService)
    {
        _auditService = auditService;
    }

    public Task WriteStartedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        string? label,
        string issuer,
        CancellationToken cancellationToken)
    {
        return _auditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "totp_enrollment.started",
                SubjectType = "totp_enrollment",
                SubjectId = enrollment.EnrollmentId.ToString("D"),
                Summary = "TOTP enrollment started.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    enrollmentId = enrollment.EnrollmentId,
                    tenantId,
                    applicationClientId,
                    externalUserId,
                    label,
                    issuer,
                    status = "pending",
                }),
                Severity = "info",
                Source = "api",
            },
            cancellationToken);
    }

    public Task WriteConfirmedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        return _auditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "totp_enrollment.confirmed",
                SubjectType = "totp_enrollment",
                SubjectId = enrollment.EnrollmentId.ToString("D"),
                Summary = "TOTP enrollment confirmed.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    enrollmentId = enrollment.EnrollmentId,
                    tenantId,
                    applicationClientId,
                    externalUserId,
                    status = "confirmed",
                }),
                Severity = "info",
                Source = "api",
            },
            cancellationToken);
    }

    public Task WriteRevokedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        return _auditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "totp_enrollment.revoked",
                SubjectType = "totp_enrollment",
                SubjectId = enrollment.EnrollmentId.ToString("D"),
                Summary = "TOTP enrollment revoked.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    enrollmentId = enrollment.EnrollmentId,
                    tenantId,
                    applicationClientId,
                    externalUserId,
                    status = "revoked",
                }),
                Severity = "warning",
                Source = "api",
            },
            cancellationToken);
    }

    public Task WriteReplacementStartedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        string? label,
        string issuer,
        CancellationToken cancellationToken)
    {
        return _auditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "totp_enrollment.replacement_started",
                SubjectType = "totp_enrollment",
                SubjectId = enrollment.EnrollmentId.ToString("D"),
                Summary = "TOTP enrollment replacement started.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    enrollmentId = enrollment.EnrollmentId,
                    tenantId,
                    applicationClientId,
                    externalUserId,
                    label,
                    issuer,
                    hasPendingReplacement = true,
                }),
                Severity = "info",
                Source = "api",
            },
            cancellationToken);
    }

    public Task WriteReplacementConfirmedAsync(
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        return _auditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "totp_enrollment.replacement_confirmed",
                SubjectType = "totp_enrollment",
                SubjectId = enrollment.EnrollmentId.ToString("D"),
                Summary = "TOTP enrollment replacement confirmed.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    enrollmentId = enrollment.EnrollmentId,
                    tenantId,
                    applicationClientId,
                    externalUserId,
                    hasPendingReplacement = false,
                    status = "confirmed",
                }),
                Severity = "info",
                Source = "api",
            },
            cancellationToken);
    }

    public Task WriteReplacementConfirmationFailedAsync(
        Guid enrollmentId,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        int failedAttempts,
        bool attemptLimitReached,
        CancellationToken cancellationToken)
    {
        return _auditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = attemptLimitReached
                    ? "totp_enrollment.replacement_locked"
                    : "totp_enrollment.replacement_confirmation_failed",
                SubjectType = "totp_enrollment",
                SubjectId = enrollmentId.ToString("D"),
                Summary = attemptLimitReached
                    ? "TOTP enrollment replacement locked after invalid attempts."
                    : "TOTP enrollment replacement confirmation failed.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    enrollmentId,
                    tenantId,
                    applicationClientId,
                    externalUserId,
                    failedAttempts,
                    attemptLimitReached,
                }),
                Severity = attemptLimitReached ? "warning" : "info",
                Source = "api",
            },
            cancellationToken);
    }

    public Task WriteConfirmationFailedAsync(
        Guid enrollmentId,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        int failedAttempts,
        bool attemptLimitReached,
        CancellationToken cancellationToken)
    {
        return _auditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = attemptLimitReached
                    ? "totp_enrollment.confirmation_locked"
                    : "totp_enrollment.confirmation_failed",
                SubjectType = "totp_enrollment",
                SubjectId = enrollmentId.ToString("D"),
                Summary = attemptLimitReached
                    ? "TOTP enrollment confirmation locked after invalid attempts."
                    : "TOTP enrollment confirmation failed.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    enrollmentId,
                    tenantId,
                    applicationClientId,
                    externalUserId,
                    failedAttempts,
                    attemptLimitReached,
                }),
                Severity = attemptLimitReached ? "warning" : "info",
                Source = "api",
            },
            cancellationToken);
    }
}
