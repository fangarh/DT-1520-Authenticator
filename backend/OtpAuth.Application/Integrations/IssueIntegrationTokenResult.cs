namespace OtpAuth.Application.Integrations;

public sealed record IssueIntegrationTokenResult
{
    public required bool IsSuccess { get; init; }

    public IssuedAccessToken? Token { get; init; }

    public IssueIntegrationTokenErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static IssueIntegrationTokenResult Success(IssuedAccessToken token) => new()
    {
        IsSuccess = true,
        Token = token,
    };

    public static IssueIntegrationTokenResult Failure(
        IssueIntegrationTokenErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum IssueIntegrationTokenErrorCode
{
    ValidationFailed = 1,
    InvalidClient = 2,
    InvalidScope = 3,
}
