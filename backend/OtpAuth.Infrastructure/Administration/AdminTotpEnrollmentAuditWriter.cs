using System.Text.Json;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Enrollments;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Administration;

public sealed class AdminTotpEnrollmentAuditWriter : IAdminTotpEnrollmentAuditWriter
{
    private readonly SecurityAuditService _securityAuditService;

    public AdminTotpEnrollmentAuditWriter(SecurityAuditService securityAuditService)
    {
        _securityAuditService = securityAuditService;
    }

    public Task WriteStartedAsync(
        AdminContext adminContext,
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        string? label,
        string issuer,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            adminContext,
            "admin_totp_enrollment.started",
            enrollment.EnrollmentId,
            "Admin started TOTP enrollment.",
            new
            {
                enrollmentId = enrollment.EnrollmentId,
                tenantId,
                applicationClientId,
                externalUserId,
                label,
                issuer,
                status = "pending",
            },
            "info",
            cancellationToken);
    }

    public Task WriteConfirmedAsync(
        AdminContext adminContext,
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        bool isReplacement,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            adminContext,
            isReplacement
                ? "admin_totp_enrollment.replacement_confirmed"
                : "admin_totp_enrollment.confirmed",
            enrollment.EnrollmentId,
            isReplacement
                ? "Admin confirmed TOTP replacement enrollment."
                : "Admin confirmed TOTP enrollment.",
            new
            {
                enrollmentId = enrollment.EnrollmentId,
                tenantId,
                applicationClientId,
                externalUserId,
                hasPendingReplacement = false,
                status = "confirmed",
            },
            "info",
            cancellationToken);
    }

    public Task WriteRevokedAsync(
        AdminContext adminContext,
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            adminContext,
            "admin_totp_enrollment.revoked",
            enrollment.EnrollmentId,
            "Admin revoked TOTP enrollment.",
            new
            {
                enrollmentId = enrollment.EnrollmentId,
                tenantId,
                applicationClientId,
                externalUserId,
                status = "revoked",
            },
            "warning",
            cancellationToken);
    }

    public Task WriteReplacementStartedAsync(
        AdminContext adminContext,
        TotpEnrollmentView enrollment,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        string? label,
        string issuer,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            adminContext,
            "admin_totp_enrollment.replacement_started",
            enrollment.EnrollmentId,
            "Admin started TOTP replacement enrollment.",
            new
            {
                enrollmentId = enrollment.EnrollmentId,
                tenantId,
                applicationClientId,
                externalUserId,
                label,
                issuer,
                hasPendingReplacement = true,
            },
            "info",
            cancellationToken);
    }

    public Task WriteConfirmationFailedAsync(
        AdminContext adminContext,
        Guid enrollmentId,
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        int failedAttempts,
        bool attemptLimitReached,
        bool isReplacement,
        CancellationToken cancellationToken)
    {
        var eventType = isReplacement
            ? attemptLimitReached
                ? "admin_totp_enrollment.replacement_locked"
                : "admin_totp_enrollment.replacement_confirmation_failed"
            : attemptLimitReached
                ? "admin_totp_enrollment.confirmation_locked"
                : "admin_totp_enrollment.confirmation_failed";
        var summary = isReplacement
            ? attemptLimitReached
                ? "Admin locked TOTP replacement confirmation after invalid attempts."
                : "Admin failed TOTP replacement confirmation."
            : attemptLimitReached
                ? "Admin locked TOTP enrollment confirmation after invalid attempts."
                : "Admin failed TOTP enrollment confirmation.";

        return RecordAsync(
            adminContext,
            eventType,
            enrollmentId,
            summary,
            new
            {
                enrollmentId,
                tenantId,
                applicationClientId,
                externalUserId,
                failedAttempts,
                attemptLimitReached,
            },
            attemptLimitReached ? "warning" : "info",
            cancellationToken);
    }

    private Task RecordAsync(
        AdminContext adminContext,
        string eventType,
        Guid enrollmentId,
        string summary,
        object payload,
        string severity,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = eventType,
                SubjectType = "totp_enrollment",
                SubjectId = enrollmentId.ToString("D"),
                Summary = summary,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId = adminContext.AdminUserId,
                    adminUsername = adminContext.Username,
                    action = payload,
                }),
                Severity = severity,
                Source = "admin_api",
            },
            cancellationToken);
    }
}
