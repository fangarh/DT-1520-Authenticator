namespace OtpAuth.Application.Integrations;

public sealed record IntrospectIntegrationTokenResult
{
    public required bool IsSuccess { get; init; }

    public IntrospectIntegrationTokenErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IntegrationAccessTokenIntrospectionResult? Introspection { get; init; }

    public static IntrospectIntegrationTokenResult Success(IntegrationAccessTokenIntrospectionResult introspection) => new()
    {
        IsSuccess = true,
        Introspection = introspection,
    };

    public static IntrospectIntegrationTokenResult Failure(
        IntrospectIntegrationTokenErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum IntrospectIntegrationTokenErrorCode
{
    ValidationFailed = 1,
    InvalidClient = 2,
}
