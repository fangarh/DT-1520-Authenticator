namespace OtpAuth.Application.Enrollments;

public enum RevokeTotpEnrollmentErrorCode
{
    None = 0,
    AccessDenied = 1,
    NotFound = 2,
    Conflict = 3,
}

public sealed record RevokeTotpEnrollmentResult
{
    public bool IsSuccess { get; init; }

    public RevokeTotpEnrollmentErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public TotpEnrollmentView? Enrollment { get; init; }

    public static RevokeTotpEnrollmentResult Success(TotpEnrollmentView enrollment) => new()
    {
        IsSuccess = true,
        Enrollment = enrollment,
    };

    public static RevokeTotpEnrollmentResult Failure(RevokeTotpEnrollmentErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
