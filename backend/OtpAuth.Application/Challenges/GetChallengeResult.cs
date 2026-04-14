using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public sealed record GetChallengeResult
{
    public required bool IsSuccess { get; init; }

    public Challenge? Challenge { get; init; }

    public GetChallengeErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static GetChallengeResult Success(Challenge challenge) => new()
    {
        IsSuccess = true,
        Challenge = challenge,
    };

    public static GetChallengeResult Failure(GetChallengeErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum GetChallengeErrorCode
{
    ValidationFailed = 1,
    NotFound = 2,
    AccessDenied = 3,
}
