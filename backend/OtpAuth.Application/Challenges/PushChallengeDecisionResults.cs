using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public enum ApprovePushChallengeErrorCode
{
    None = 0,
    ValidationFailed = 1,
    NotFound = 2,
    InvalidState = 3,
    ChallengeExpired = 4,
    PolicyDenied = 5,
}

public sealed record ApprovePushChallengeResult
{
    public required bool IsSuccess { get; init; }

    public Challenge? Challenge { get; init; }

    public ApprovePushChallengeErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static ApprovePushChallengeResult Success(Challenge challenge) => new()
    {
        IsSuccess = true,
        Challenge = challenge,
    };

    public static ApprovePushChallengeResult Failure(
        ApprovePushChallengeErrorCode errorCode,
        string errorMessage,
        Challenge? challenge = null) => new()
    {
        IsSuccess = false,
        Challenge = challenge,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum DenyPushChallengeErrorCode
{
    None = 0,
    ValidationFailed = 1,
    NotFound = 2,
    InvalidState = 3,
    ChallengeExpired = 4,
}

public sealed record DenyPushChallengeResult
{
    public required bool IsSuccess { get; init; }

    public Challenge? Challenge { get; init; }

    public DenyPushChallengeErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static DenyPushChallengeResult Success(Challenge challenge) => new()
    {
        IsSuccess = true,
        Challenge = challenge,
    };

    public static DenyPushChallengeResult Failure(
        DenyPushChallengeErrorCode errorCode,
        string errorMessage,
        Challenge? challenge = null) => new()
    {
        IsSuccess = false,
        Challenge = challenge,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
