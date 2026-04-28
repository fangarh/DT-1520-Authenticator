using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Administration;

public enum AdminDeviceOnboardingStatus
{
    Pending = 0,
    Consumed = 1,
    Expired = 2,
    Revoked = 3,
}

public sealed record AdminDeviceOnboardingListRequest
{
    public required Guid TenantId { get; init; }

    public string? ExternalUserId { get; init; }

    public Guid? ApplicationClientId { get; init; }

    public AdminDeviceOnboardingStatus? Status { get; init; }

    public int Limit { get; init; } = 50;
}

public sealed record AdminDeviceOnboardingCreateRequest
{
    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public int TtlMinutes { get; init; } = 10;

    public bool HasOperatorProvidedActivationPayload { get; init; }
}

public sealed record AdminDeviceOnboardingRouteRequest
{
    public required Guid TenantId { get; init; }

    public required Guid ActivationCodeId { get; init; }
}

public sealed record AdminDeviceOnboardingView
{
    public required Guid ActivationCodeId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required AdminDeviceOnboardingStatus Status { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }

    public DateTimeOffset? ConsumedUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

public sealed record AdminDeviceOnboardingCreateDraft
{
    public required Guid ActivationCodeId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required DevicePlatform Platform { get; init; }

    public required string CodeHash { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

public sealed record AdminDeviceOnboardingRevokeStoreResult
{
    public required bool IsFound { get; init; }

    public required bool WasRevoked { get; init; }

    public AdminDeviceOnboardingView? Artifact { get; init; }
}

public enum AdminListDeviceOnboardingArtifactsErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
}

public sealed record AdminListDeviceOnboardingArtifactsResult
{
    public bool IsSuccess { get; init; }

    public AdminListDeviceOnboardingArtifactsErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyCollection<AdminDeviceOnboardingView> Artifacts { get; init; } = Array.Empty<AdminDeviceOnboardingView>();

    public static AdminListDeviceOnboardingArtifactsResult Success(IReadOnlyCollection<AdminDeviceOnboardingView> artifacts) => new()
    {
        IsSuccess = true,
        Artifacts = artifacts,
    };

    public static AdminListDeviceOnboardingArtifactsResult Failure(
        AdminListDeviceOnboardingArtifactsErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminCreateDeviceOnboardingArtifactErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    Conflict = 2,
}

public sealed record AdminCreateDeviceOnboardingArtifactResult
{
    public bool IsSuccess { get; init; }

    public AdminCreateDeviceOnboardingArtifactErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminDeviceOnboardingView? Artifact { get; init; }

    public string? ActivationPayload { get; init; }

    public static AdminCreateDeviceOnboardingArtifactResult Success(
        AdminDeviceOnboardingView artifact,
        string activationPayload) => new()
    {
        IsSuccess = true,
        Artifact = artifact,
        ActivationPayload = activationPayload,
    };

    public static AdminCreateDeviceOnboardingArtifactResult Failure(
        AdminCreateDeviceOnboardingArtifactErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminRevokeDeviceOnboardingArtifactErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
    Conflict = 3,
}

public sealed record AdminRevokeDeviceOnboardingArtifactResult
{
    public bool IsSuccess { get; init; }

    public AdminRevokeDeviceOnboardingArtifactErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminDeviceOnboardingView? Artifact { get; init; }

    public static AdminRevokeDeviceOnboardingArtifactResult Success(AdminDeviceOnboardingView artifact) => new()
    {
        IsSuccess = true,
        Artifact = artifact,
    };

    public static AdminRevokeDeviceOnboardingArtifactResult Failure(
        AdminRevokeDeviceOnboardingArtifactErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public interface IAdminDeviceOnboardingStore
{
    Task<IReadOnlyCollection<AdminDeviceOnboardingView>> ListAsync(
        AdminDeviceOnboardingListRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<AdminDeviceOnboardingView?> CreateAsync(
        AdminDeviceOnboardingCreateDraft draft,
        CancellationToken cancellationToken);

    Task<AdminDeviceOnboardingRevokeStoreResult> RevokeAsync(
        Guid tenantId,
        Guid activationCodeId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken);
}
