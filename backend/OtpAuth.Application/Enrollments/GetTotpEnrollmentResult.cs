namespace OtpAuth.Application.Enrollments;

public enum GetTotpEnrollmentErrorCode
{
    None = 0,
    AccessDenied = 1,
    NotFound = 2,
}

public sealed record GetTotpEnrollmentResult
{
    public bool IsSuccess { get; init; }

    public GetTotpEnrollmentErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public TotpEnrollmentView? Enrollment { get; init; }

    public static GetTotpEnrollmentResult Success(TotpEnrollmentView enrollment) => new()
    {
        IsSuccess = true,
        Enrollment = enrollment,
    };

    public static GetTotpEnrollmentResult Failure(GetTotpEnrollmentErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
