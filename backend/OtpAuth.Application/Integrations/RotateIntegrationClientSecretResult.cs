namespace OtpAuth.Application.Integrations;

public sealed record RotateIntegrationClientSecretResult
{
    public required bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public string? NewClientSecret { get; init; }

    public DateTimeOffset? RotatedAtUtc { get; init; }

    public static RotateIntegrationClientSecretResult Success(string newClientSecret, DateTimeOffset rotatedAtUtc) => new()
    {
        IsSuccess = true,
        NewClientSecret = newClientSecret,
        RotatedAtUtc = rotatedAtUtc,
    };

    public static RotateIntegrationClientSecretResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
    };
}
