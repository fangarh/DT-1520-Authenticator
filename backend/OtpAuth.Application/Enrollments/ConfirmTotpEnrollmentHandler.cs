using OtpAuth.Application.Factors;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Enrollments;

public sealed class ConfirmTotpEnrollmentHandler
{
    private const int MaxFailedConfirmationAttempts = 5;

    private readonly ITotpEnrollmentProvisioningStore _provisioningStore;
    private readonly ITotpEnrollmentAuditWriter _auditWriter;

    public ConfirmTotpEnrollmentHandler(
        ITotpEnrollmentProvisioningStore provisioningStore,
        ITotpEnrollmentAuditWriter auditWriter)
    {
        _provisioningStore = provisioningStore;
        _auditWriter = auditWriter;
    }

    public async Task<ConfirmTotpEnrollmentResult> HandleAsync(
        ConfirmTotpEnrollmentRequest request,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return ConfirmTotpEnrollmentResult.Failure(ConfirmTotpEnrollmentErrorCode.ValidationFailed, validationError);
        }

        var accessError = ValidateAccess(clientContext);
        if (accessError is not null)
        {
            return ConfirmTotpEnrollmentResult.Failure(ConfirmTotpEnrollmentErrorCode.AccessDenied, accessError);
        }

        var enrollment = await _provisioningStore.GetByIdAsync(
            request.EnrollmentId,
            clientContext.TenantId,
            clientContext.ApplicationClientId,
            cancellationToken);
        if (enrollment is null || !enrollment.IsActive)
        {
            return ConfirmTotpEnrollmentResult.Failure(
                ConfirmTotpEnrollmentErrorCode.NotFound,
                $"Enrollment '{request.EnrollmentId}' was not found.");
        }

        if (enrollment.PendingReplacement is null && enrollment.ConfirmedUtc.HasValue)
        {
            return ConfirmTotpEnrollmentResult.Failure(
                ConfirmTotpEnrollmentErrorCode.Conflict,
                $"Enrollment '{request.EnrollmentId}' is already confirmed.");
        }

        if (enrollment.PendingReplacement is null &&
            enrollment.FailedConfirmationAttempts >= MaxFailedConfirmationAttempts)
        {
            return ConfirmTotpEnrollmentResult.Failure(
                ConfirmTotpEnrollmentErrorCode.Conflict,
                "Too many invalid confirmation attempts. Restart enrollment.");
        }

        if (enrollment.PendingReplacement is not null &&
            enrollment.PendingReplacement.FailedConfirmationAttempts >= MaxFailedConfirmationAttempts)
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
            if (enrollment.PendingReplacement is not null)
            {
                await _provisioningStore.IncrementFailedReplacementConfirmationAttemptsAsync(enrollment.EnrollmentId, cancellationToken);

                var failedAttempts = enrollment.PendingReplacement.FailedConfirmationAttempts + 1;
                var attemptLimitReached = failedAttempts >= MaxFailedConfirmationAttempts;
                await _auditWriter.WriteReplacementConfirmationFailedAsync(
                    enrollment.EnrollmentId,
                    enrollment.TenantId,
                    enrollment.ApplicationClientId,
                    enrollment.ExternalUserId,
                    failedAttempts,
                    attemptLimitReached,
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

            return ConfirmTotpEnrollmentResult.Failure(
                initialAttemptLimitReached
                    ? ConfirmTotpEnrollmentErrorCode.Conflict
                    : ConfirmTotpEnrollmentErrorCode.ValidationFailed,
                initialAttemptLimitReached
                    ? "Too many invalid confirmation attempts. Restart enrollment."
                    : "Invalid one-time password.");
        }

        var confirmed = enrollment.PendingReplacement is not null
            ? await _provisioningStore.ConfirmReplacementAsync(enrollment.EnrollmentId, timestamp, cancellationToken)
            : await _provisioningStore.ConfirmAsync(enrollment.EnrollmentId, timestamp, cancellationToken);
        if (!confirmed)
        {
            return ConfirmTotpEnrollmentResult.Failure(
                ConfirmTotpEnrollmentErrorCode.Conflict,
                enrollment.PendingReplacement is not null
                    ? $"Enrollment '{request.EnrollmentId}' replacement is no longer pending."
                    : $"Enrollment '{request.EnrollmentId}' is already confirmed.");
        }

        var response = new TotpEnrollmentView
        {
            EnrollmentId = enrollment.EnrollmentId,
            Status = TotpEnrollmentStatus.Confirmed,
            HasPendingReplacement = false,
        };

        if (enrollment.PendingReplacement is not null)
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

    private static string? ValidateAccess(IntegrationClientContext clientContext)
    {
        return clientContext.HasScope(IntegrationClientScopes.EnrollmentsWrite)
            ? null
            : $"Scope '{IntegrationClientScopes.EnrollmentsWrite}' is required.";
    }
}
