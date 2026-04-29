using OtpAuth.Application.Enrollments;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Administration;

public sealed class AdminCreateCombinedOnboardingPackageHandler
{
    private readonly AdminStartTotpEnrollmentHandler _totpEnrollmentHandler;
    private readonly AdminCreateDeviceOnboardingArtifactHandler _deviceOnboardingHandler;

    public AdminCreateCombinedOnboardingPackageHandler(
        AdminStartTotpEnrollmentHandler totpEnrollmentHandler,
        AdminCreateDeviceOnboardingArtifactHandler deviceOnboardingHandler)
    {
        _totpEnrollmentHandler = totpEnrollmentHandler;
        _deviceOnboardingHandler = deviceOnboardingHandler;
    }

    public async Task<AdminCreateCombinedOnboardingPackageResult> HandleAsync(
        AdminCreateCombinedOnboardingPackageRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accessError = ValidateAccess(adminContext);
        if (accessError is not null)
        {
            return AdminCreateCombinedOnboardingPackageResult.Failure(
                AdminCreateCombinedOnboardingPackageErrorCode.AccessDenied,
                accessError);
        }

        var totpResult = await _totpEnrollmentHandler.HandleAsync(
            new AdminStartTotpEnrollmentRequest
            {
                TenantId = request.TenantId,
                ApplicationClientId = request.ApplicationClientId,
                ExternalUserId = request.ExternalUserId,
                Issuer = request.Issuer,
                Label = request.Label,
            },
            adminContext,
            cancellationToken);
        if (!totpResult.IsSuccess || totpResult.Enrollment is null)
        {
            return AdminCreateCombinedOnboardingPackageResult.Failure(
                MapTotpError(totpResult.ErrorCode),
                totpResult.ErrorMessage ?? "TOTP enrollment could not be started.");
        }

        var deviceResult = await _deviceOnboardingHandler.HandleAsync(
            new AdminDeviceOnboardingCreateRequest
            {
                TenantId = request.TenantId,
                ApplicationClientId = request.ApplicationClientId,
                ExternalUserId = request.ExternalUserId,
                Platform = request.Platform,
                TtlMinutes = request.TtlMinutes,
            },
            adminContext,
            cancellationToken);
        if (!deviceResult.IsSuccess || deviceResult.Artifact is null || deviceResult.ActivationPayload is null)
        {
            return AdminCreateCombinedOnboardingPackageResult.Failure(
                MapDeviceError(deviceResult.ErrorCode),
                deviceResult.ErrorMessage ?? "Device onboarding artifact could not be created.");
        }

        return AdminCreateCombinedOnboardingPackageResult.Success(
            deviceResult.Artifact,
            deviceResult.ActivationPayload,
            totpResult.Enrollment);
    }

    private static string? ValidateAccess(AdminContext adminContext)
    {
        if (!adminContext.HasPermission(AdminPermissions.DevicesWrite))
        {
            return $"Permission '{AdminPermissions.DevicesWrite}' is required.";
        }

        return adminContext.HasPermission(AdminPermissions.EnrollmentsWrite)
            ? null
            : $"Permission '{AdminPermissions.EnrollmentsWrite}' is required.";
    }

    private static AdminCreateCombinedOnboardingPackageErrorCode MapTotpError(AdminStartTotpEnrollmentErrorCode errorCode)
    {
        return errorCode switch
        {
            AdminStartTotpEnrollmentErrorCode.AccessDenied => AdminCreateCombinedOnboardingPackageErrorCode.AccessDenied,
            AdminStartTotpEnrollmentErrorCode.NotFound => AdminCreateCombinedOnboardingPackageErrorCode.NotFound,
            AdminStartTotpEnrollmentErrorCode.PolicyDenied => AdminCreateCombinedOnboardingPackageErrorCode.PolicyDenied,
            AdminStartTotpEnrollmentErrorCode.Conflict => AdminCreateCombinedOnboardingPackageErrorCode.Conflict,
            _ => AdminCreateCombinedOnboardingPackageErrorCode.ValidationFailed,
        };
    }

    private static AdminCreateCombinedOnboardingPackageErrorCode MapDeviceError(
        AdminCreateDeviceOnboardingArtifactErrorCode? errorCode)
    {
        return errorCode switch
        {
            AdminCreateDeviceOnboardingArtifactErrorCode.AccessDenied => AdminCreateCombinedOnboardingPackageErrorCode.AccessDenied,
            AdminCreateDeviceOnboardingArtifactErrorCode.Conflict => AdminCreateCombinedOnboardingPackageErrorCode.Conflict,
            _ => AdminCreateCombinedOnboardingPackageErrorCode.ValidationFailed,
        };
    }
}

public sealed record AdminCreateCombinedOnboardingPackageRequest
{
    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public int TtlMinutes { get; init; } = AdminDeviceOnboardingValidation.DefaultTtlMinutes;

    public string? Issuer { get; init; }

    public string? Label { get; init; }
}

public enum AdminCreateCombinedOnboardingPackageErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
    PolicyDenied = 3,
    Conflict = 4,
}

public sealed record AdminCreateCombinedOnboardingPackageResult
{
    public bool IsSuccess { get; init; }

    public AdminCreateCombinedOnboardingPackageErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminDeviceOnboardingView? DeviceArtifact { get; init; }

    public string? ActivationPayload { get; init; }

    public TotpEnrollmentView? TotpEnrollment { get; init; }

    public static AdminCreateCombinedOnboardingPackageResult Success(
        AdminDeviceOnboardingView deviceArtifact,
        string activationPayload,
        TotpEnrollmentView totpEnrollment) => new()
    {
        IsSuccess = true,
        DeviceArtifact = deviceArtifact,
        ActivationPayload = activationPayload,
        TotpEnrollment = totpEnrollment,
    };

    public static AdminCreateCombinedOnboardingPackageResult Failure(
        AdminCreateCombinedOnboardingPackageErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
