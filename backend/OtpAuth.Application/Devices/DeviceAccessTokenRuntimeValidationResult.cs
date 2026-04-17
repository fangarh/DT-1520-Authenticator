namespace OtpAuth.Application.Devices;

public sealed record DeviceAccessTokenRuntimeValidationResult
{
    public bool IsValid { get; init; }

    public string? ErrorMessage { get; init; }

    public static DeviceAccessTokenRuntimeValidationResult Success() => new()
    {
        IsValid = true,
    };

    public static DeviceAccessTokenRuntimeValidationResult Failure(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage,
    };
}
