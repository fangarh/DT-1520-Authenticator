using OtpAuth.Application.Enrollments;

namespace OtpAuth.Application.Administration;

public enum AdminStartTotpEnrollmentErrorCode
{
    None = 0,
    ValidationFailed = 1,
    AccessDenied = 2,
    NotFound = 3,
    PolicyDenied = 4,
    Conflict = 5,
}

public sealed record AdminStartTotpEnrollmentResult
{
    public bool IsSuccess { get; init; }

    public AdminStartTotpEnrollmentErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public TotpEnrollmentView? Enrollment { get; init; }

    public static AdminStartTotpEnrollmentResult Success(TotpEnrollmentView enrollment) => new()
    {
        IsSuccess = true,
        Enrollment = enrollment,
    };

    public static AdminStartTotpEnrollmentResult Failure(
        AdminStartTotpEnrollmentErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
