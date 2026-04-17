namespace OtpAuth.Application.Enrollments;

public enum ReplaceTotpEnrollmentErrorCode
{
    None = 0,
    AccessDenied = 1,
    NotFound = 2,
    Conflict = 3,
}

public sealed record ReplaceTotpEnrollmentResult
{
    public bool IsSuccess { get; init; }

    public ReplaceTotpEnrollmentErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public TotpEnrollmentView? Enrollment { get; init; }

    public static ReplaceTotpEnrollmentResult Success(TotpEnrollmentView enrollment) => new()
    {
        IsSuccess = true,
        Enrollment = enrollment,
    };

    public static ReplaceTotpEnrollmentResult Failure(ReplaceTotpEnrollmentErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
