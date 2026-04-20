namespace OtpAuth.Application.Devices;

public enum ActivateDeviceErrorCode
{
    None = 0,
    ValidationFailed = 1,
    AccessDenied = 2,
    InvalidActivationCode = 3,
    Conflict = 4,
}

public sealed record ActivateDeviceResult
{
    public bool IsSuccess { get; init; }

    public ActivateDeviceErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public DeviceView? Device { get; init; }

    public DeviceTokenPair? Tokens { get; init; }

    public static ActivateDeviceResult Success(DeviceView device, DeviceTokenPair tokens) => new()
    {
        IsSuccess = true,
        Device = device,
        Tokens = tokens,
    };

    public static ActivateDeviceResult Failure(ActivateDeviceErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum RefreshDeviceTokenErrorCode
{
    None = 0,
    ValidationFailed = 1,
    InvalidToken = 2,
    Conflict = 3,
}

public sealed record RefreshDeviceTokenResult
{
    public bool IsSuccess { get; init; }

    public RefreshDeviceTokenErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public DeviceTokenPair? Tokens { get; init; }

    public static RefreshDeviceTokenResult Success(DeviceTokenPair tokens) => new()
    {
        IsSuccess = true,
        Tokens = tokens,
    };

    public static RefreshDeviceTokenResult Failure(RefreshDeviceTokenErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum RevokeDeviceErrorCode
{
    None = 0,
    ValidationFailed = 1,
    AccessDenied = 2,
    NotFound = 3,
}

public sealed record RevokeDeviceResult
{
    public bool IsSuccess { get; init; }

    public RevokeDeviceErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public DeviceView? Device { get; init; }

    public bool WasStateChanged { get; init; }

    public static RevokeDeviceResult Success(DeviceView device, bool wasStateChanged) => new()
    {
        IsSuccess = true,
        Device = device,
        WasStateChanged = wasStateChanged,
    };

    public static RevokeDeviceResult Failure(RevokeDeviceErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum ListDevicesForRoutingErrorCode
{
    None = 0,
    ValidationFailed = 1,
    AccessDenied = 2,
}

public sealed record ListDevicesForRoutingResult
{
    public bool IsSuccess { get; init; }

    public ListDevicesForRoutingErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyCollection<DeviceView> Devices { get; init; } = Array.Empty<DeviceView>();

    public static ListDevicesForRoutingResult Success(IReadOnlyCollection<DeviceView> devices) => new()
    {
        IsSuccess = true,
        Devices = devices,
    };

    public static ListDevicesForRoutingResult Failure(
        ListDevicesForRoutingErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum ListPendingPushChallengesForDeviceErrorCode
{
    None = 0,
    AccessDenied = 1,
}

public sealed record ListPendingPushChallengesForDeviceResult
{
    public bool IsSuccess { get; init; }

    public ListPendingPushChallengesForDeviceErrorCode ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyCollection<Domain.Challenges.Challenge> Challenges { get; init; } = Array.Empty<Domain.Challenges.Challenge>();

    public static ListPendingPushChallengesForDeviceResult Success(
        IReadOnlyCollection<Domain.Challenges.Challenge> challenges) => new()
    {
        IsSuccess = true,
        Challenges = challenges,
    };

    public static ListPendingPushChallengesForDeviceResult Failure(
        ListPendingPushChallengesForDeviceErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}
