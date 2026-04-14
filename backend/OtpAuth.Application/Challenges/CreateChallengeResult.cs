using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public sealed record CreateChallengeResult
{
    public required bool IsSuccess { get; init; }

    public Challenge? Challenge { get; init; }

    public CreateChallengeErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static CreateChallengeResult Success(Challenge challenge) => new()
    {
        IsSuccess = true,
        Challenge = challenge,
    };

    public static CreateChallengeResult Failure(CreateChallengeErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum CreateChallengeErrorCode
{
    ValidationFailed = 1,
    PolicyDenied = 2,
    AccessDenied = 3,
}
