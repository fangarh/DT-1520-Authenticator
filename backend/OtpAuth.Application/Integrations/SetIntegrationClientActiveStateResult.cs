namespace OtpAuth.Application.Integrations;

public sealed record SetIntegrationClientActiveStateResult
{
    public required bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public bool? IsActive { get; init; }

    public DateTimeOffset? ChangedAtUtc { get; init; }

    public bool WasStateChanged { get; init; }

    public static SetIntegrationClientActiveStateResult Success(
        bool isActive,
        DateTimeOffset changedAtUtc,
        bool wasStateChanged = true) => new()
    {
        IsSuccess = true,
        IsActive = isActive,
        ChangedAtUtc = changedAtUtc,
        WasStateChanged = wasStateChanged,
    };

    public static SetIntegrationClientActiveStateResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage,
    };
}
