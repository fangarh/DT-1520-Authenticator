namespace OtpAuth.Application.Enrollments;

public enum GetCurrentTotpEnrollmentForAdminErrorCode
{
    None = 0,
    ValidationFailed = 1,
    AccessDenied = 2,
    NotFound = 3,
}

public sealed record GetCurrentTotpEnrollmentForAdminResult
{
    public bool IsSuccess { get; init; }

    public GetCurrentTotpEnrollmentForAdminErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public TotpEnrollmentAdminView? Enrollment { get; init; }

    public static GetCurrentTotpEnrollmentForAdminResult Success(TotpEnrollmentAdminView enrollment) => new()
    {
        IsSuccess = true,
        Enrollment = enrollment,
    };

    public static GetCurrentTotpEnrollmentForAdminResult Failure(
        GetCurrentTotpEnrollmentForAdminErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
