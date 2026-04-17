namespace OtpAuth.Application.Administration;

public enum AdminLoginErrorCode
{
    None = 0,
    ValidationFailed = 1,
    InvalidCredentials = 2,
    RateLimited = 3,
}

public sealed record AdminLoginResult
{
    public bool IsSuccess { get; init; }

    public AdminAuthenticatedUser? User { get; init; }

    public AdminLoginErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public int? RetryAfterSeconds { get; init; }

    public static AdminLoginResult Success(AdminAuthenticatedUser user) => new()
    {
        IsSuccess = true,
        User = user,
        ErrorCode = AdminLoginErrorCode.None,
    };

    public static AdminLoginResult Failure(
        AdminLoginErrorCode errorCode,
        string errorMessage,
        int? retryAfterSeconds = null) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        RetryAfterSeconds = retryAfterSeconds,
    };
}
