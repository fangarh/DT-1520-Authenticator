using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Factors;

namespace OtpAuth.Application.Administration;

public sealed class AdminConfirmTotpEnrollmentHandler
{
    private const int MaxFailedConfirmationAttempts = 5;

    private readonly ITotpEnrollmentProvisioningStore _provisioningStore;
    private readonly ITotpEnrollmentAuditWriter _auditWriter;
    private readonly IAdminTotpEnrollmentAuditWriter _adminAuditWriter;

    public AdminConfirmTotpEnrollmentHandler(
        ITotpEnrollmentProvisioningStore provisioningStore,
        ITotpEnrollmentAuditWriter auditWriter,
        IAdminTotpEnrollmentAuditWriter adminAuditWriter)
    {
        _provisioningStore = provisioningStore;
        _auditWriter = auditWriter;
        _adminAuditWriter = adminAuditWriter;
    }

    public async Task<ConfirmTotpEnrollmentResult> HandleAsync(
        ConfirmTotpEnrollmentRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return ConfirmTotpEnrollmentResult.Failure(ConfirmTotpEnrollmentErrorCode.ValidationFailed, validationError);
        }

        var accessError = ValidateAccess(adminContext);
        if (accessError is not null)
        {
            return ConfirmTotpEnrollmentResult.Failure(ConfirmTotpEnrollmentErrorCode.AccessDenied, accessError);
        }

        var enrollment = await _provisioningStore.GetByIdForAdminAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null || !enrollment.IsActive)
        {
            return ConfirmTotpEnrollmentResult.Failure(
                ConfirmTotpEnrollmentErrorCode.NotFound,
                $"Enrollment '{request.EnrollmentId}' was not found.");
        }

        var isReplacement = enrollment.PendingReplacement is not null;
        if (!isReplacement && enrollment.ConfirmedUtc.HasValue)
        {
            return ConfirmTotpEnrollmentResult.Failure(
                ConfirmTotpEnrollmentErrorCode.Conflict,
                $"Enrollment '{request.EnrollmentId}' is already confirmed.");
        }

        if (!isReplacement && enrollment.FailedConfirmationAttempts >= MaxFailedConfirmationAttempts)
        {
            return ConfirmTotpEnrollmentResult.Failure(
                ConfirmTotpEnrollmentErrorCode.Conflict,
                "Too many invalid confirmation attempts. Restart enrollment.");
        }

        if (isReplacement &&
            enrollment.PendingReplacement!.FailedConfirmationAttempts >= MaxFailedConfirmationAttempts)
        {
            return ConfirmTotpEnrollmentResult.Failure(
                ConfirmTotpEnrollmentErrorCode.Conflict,
                "Too many invalid replacement confirmation attempts. Restart replacement.");
        }

        var timestamp = DateTimeOffset.UtcNow;
        var verificationSecret = enrollment.PendingReplacement?.Secret ?? enrollment.Secret;
        var verificationDigits = enrollment.PendingReplacement?.Digits ?? enrollment.Digits;
        var verificationPeriodSeconds = enrollment.PendingReplacement?.PeriodSeconds ?? enrollment.PeriodSeconds;
        var verificationAlgorithm = enrollment.PendingReplacement?.Algorithm ?? enrollment.Algorithm;
        var isValid = TotpCodeCalculator.IsCodeValid(
            verificationSecret,
            verificationDigits,
            verificationPeriodSeconds,
            verificationAlgorithm,
            request.Code,
            timestamp);
        if (!isValid)
        {
            if (isReplacement)
            {
                await _provisioningStore.IncrementFailedReplacementConfirmationAttemptsAsync(enrollment.EnrollmentId, cancellationToken);

                var failedAttempts = enrollment.PendingReplacement!.FailedConfirmationAttempts + 1;
                var attemptLimitReached = failedAttempts >= MaxFailedConfirmationAttempts;
                await _auditWriter.WriteReplacementConfirmationFailedAsync(
                    enrollment.EnrollmentId,
                    enrollment.TenantId,
                    enrollment.ApplicationClientId,
                    enrollment.ExternalUserId,
                    failedAttempts,
                    attemptLimitReached,
                    cancellationToken);
                await _adminAuditWriter.WriteConfirmationFailedAsync(
                    adminContext,
                    enrollment.EnrollmentId,
                    enrollment.TenantId,
                    enrollment.ApplicationClientId,
                    enrollment.ExternalUserId,
                    failedAttempts,
                    attemptLimitReached,
                    isReplacement: true,
                    cancellationToken);

                return ConfirmTotpEnrollmentResult.Failure(
                    attemptLimitReached
                        ? ConfirmTotpEnrollmentErrorCode.Conflict
                        : ConfirmTotpEnrollmentErrorCode.ValidationFailed,
                    attemptLimitReached
                        ? "Too many invalid replacement confirmation attempts. Restart replacement."
                        : "Invalid one-time password.");
            }

            await _provisioningStore.IncrementFailedConfirmationAttemptsAsync(enrollment.EnrollmentId, cancellationToken);

            var initialFailedAttempts = enrollment.FailedConfirmationAttempts + 1;
            var initialAttemptLimitReached = initialFailedAttempts >= MaxFailedConfirmationAttempts;
            await _auditWriter.WriteConfirmationFailedAsync(
                enrollment.EnrollmentId,
                enrollment.TenantId,
                enrollment.ApplicationClientId,
                enrollment.ExternalUserId,
                initialFailedAttempts,
                initialAttemptLimitReached,
                cancellationToken);
            await _adminAuditWriter.WriteConfirmationFailedAsync(
                adminContext,
                enrollment.EnrollmentId,
                enrollment.TenantId,
                enrollment.ApplicationClientId,
                enrollment.ExternalUserId,
                initialFailedAttempts,
                initialAttemptLimitReached,
                isReplacement: false,
                cancellationToken);

            return ConfirmTotpEnrollmentResult.Failure(
                initialAttemptLimitReached
                    ? ConfirmTotpEnrollmentErrorCode.Conflict
                    : ConfirmTotpEnrollmentErrorCode.ValidationFailed,
                initialAttemptLimitReached
                    ? "Too many invalid confirmation attempts. Restart enrollment."
                    : "Invalid one-time password.");
        }

        var confirmed = isReplacement
            ? await _provisioningStore.ConfirmReplacementAsync(enrollment.EnrollmentId, timestamp, cancellationToken)
            : await _provisioningStore.ConfirmAsync(enrollment.EnrollmentId, timestamp, cancellationToken);
        if (!confirmed)
        {
            return ConfirmTotpEnrollmentResult.Failure(
                ConfirmTotpEnrollmentErrorCode.Conflict,
                isReplacement
                    ? $"Enrollment '{request.EnrollmentId}' replacement is no longer pending."
                    : $"Enrollment '{request.EnrollmentId}' is already confirmed.");
        }

        var response = new TotpEnrollmentView
        {
            EnrollmentId = enrollment.EnrollmentId,
            Status = TotpEnrollmentStatus.Confirmed,
            HasPendingReplacement = false,
        };

        if (isReplacement)
        {
            await _auditWriter.WriteReplacementConfirmedAsync(
                response,
                enrollment.TenantId,
                enrollment.ApplicationClientId,
                enrollment.ExternalUserId,
                cancellationToken);
        }
        else
        {
            await _auditWriter.WriteConfirmedAsync(
                response,
                enrollment.TenantId,
                enrollment.ApplicationClientId,
                enrollment.ExternalUserId,
                cancellationToken);
        }

        await _adminAuditWriter.WriteConfirmedAsync(
            adminContext,
            response,
            enrollment.TenantId,
            enrollment.ApplicationClientId,
            enrollment.ExternalUserId,
            isReplacement,
            cancellationToken);

        return ConfirmTotpEnrollmentResult.Success(response);
    }

    private static string? Validate(ConfirmTotpEnrollmentRequest request)
    {
        if (request.EnrollmentId == Guid.Empty)
        {
            return "EnrollmentId is required.";
        }

        var normalizedCode = request.Code?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode) ||
            normalizedCode.Length != 6 ||
            !normalizedCode.All(char.IsAsciiDigit))
        {
            return "Code must be a 6-digit numeric value.";
        }

        return null;
    }

    private static string? ValidateAccess(AdminContext adminContext)
    {
        return adminContext.HasPermission(AdminPermissions.EnrollmentsWrite)
            ? null
            : $"Permission '{AdminPermissions.EnrollmentsWrite}' is required.";
    }
}
