namespace OtpAuth.Application.Integrations;

public sealed record RevokeIntegrationTokenResult
{
    public required bool IsSuccess { get; init; }

    public RevokeIntegrationTokenErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static RevokeIntegrationTokenResult Success() => new()
    {
        IsSuccess = true,
    };

    public static RevokeIntegrationTokenResult Failure(
        RevokeIntegrationTokenErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum RevokeIntegrationTokenErrorCode
{
    ValidationFailed = 1,
    InvalidClient = 2,
}
