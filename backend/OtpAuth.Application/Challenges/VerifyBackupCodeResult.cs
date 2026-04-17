using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public sealed record VerifyBackupCodeResult
{
    public required bool IsSuccess { get; init; }

    public Challenge? Challenge { get; init; }

    public VerifyBackupCodeErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public int? RetryAfterSeconds { get; init; }

    public static VerifyBackupCodeResult Success(Challenge challenge) => new()
    {
        IsSuccess = true,
        Challenge = challenge,
    };

    public static VerifyBackupCodeResult Failure(
        VerifyBackupCodeErrorCode errorCode,
        string errorMessage,
        Challenge? challenge = null,
        int? retryAfterSeconds = null) => new()
    {
        IsSuccess = false,
        Challenge = challenge,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        RetryAfterSeconds = retryAfterSeconds,
    };
}

public enum VerifyBackupCodeErrorCode
{
    ValidationFailed = 1,
    NotFound = 2,
    InvalidState = 3,
    ChallengeExpired = 4,
    InvalidCode = 5,
    AccessDenied = 6,
    RateLimited = 7,
}
