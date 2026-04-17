namespace OtpAuth.Application.Administration;

public enum AdminApplicationClientResolutionErrorCode
{
    None = 0,
    NotFound = 1,
    Conflict = 2,
}

public sealed record AdminApplicationClientResolutionResult
{
    public bool IsSuccess { get; init; }

    public AdminApplicationClientResolutionErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public Guid? ApplicationClientId { get; init; }

    public static AdminApplicationClientResolutionResult Success(Guid applicationClientId) => new()
    {
        IsSuccess = true,
        ApplicationClientId = applicationClientId,
    };

    public static AdminApplicationClientResolutionResult Failure(
        AdminApplicationClientResolutionErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
