namespace OtpAuth.Application.Enrollments;

public enum StartTotpEnrollmentErrorCode
{
    None = 0,
    ValidationFailed = 1,
    AccessDenied = 2,
    PolicyDenied = 3,
    Conflict = 4,
}

public sealed record StartTotpEnrollmentResult
{
    public bool IsSuccess { get; init; }

    public StartTotpEnrollmentErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public TotpEnrollmentView? Enrollment { get; init; }

    public static StartTotpEnrollmentResult Success(TotpEnrollmentView enrollment) => new()
    {
        IsSuccess = true,
        Enrollment = enrollment,
    };

    public static StartTotpEnrollmentResult Failure(StartTotpEnrollmentErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum ConfirmTotpEnrollmentErrorCode
{
    None = 0,
    ValidationFailed = 1,
    AccessDenied = 2,
    NotFound = 3,
    Conflict = 4,
}

public sealed record ConfirmTotpEnrollmentResult
{
    public bool IsSuccess { get; init; }

    public ConfirmTotpEnrollmentErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public TotpEnrollmentView? Enrollment { get; init; }

    public static ConfirmTotpEnrollmentResult Success(TotpEnrollmentView enrollment) => new()
    {
        IsSuccess = true,
        Enrollment = enrollment,
    };

    public static ConfirmTotpEnrollmentResult Failure(ConfirmTotpEnrollmentErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
