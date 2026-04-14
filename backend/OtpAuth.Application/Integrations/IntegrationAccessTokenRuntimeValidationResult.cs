namespace OtpAuth.Application.Integrations;

public sealed record IntegrationAccessTokenRuntimeValidationResult
{
    public required bool IsValid { get; init; }

    public string? ErrorMessage { get; init; }

    public static IntegrationAccessTokenRuntimeValidationResult Success() => new()
    {
        IsValid = true,
    };

    public static IntegrationAccessTokenRuntimeValidationResult Failure(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage,
    };
}
