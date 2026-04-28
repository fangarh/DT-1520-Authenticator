namespace OtpAuth.Application.Administration;

public enum AdminIntegrationClientStatus
{
    Active = 0,
    Inactive = 1,
}

public sealed record AdminIntegrationClientListRequest
{
    public required Guid TenantId { get; init; }
}

public sealed record AdminIntegrationClientCreateRequest
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();

    public bool HasOperatorProvidedSecret { get; init; }
}

public sealed record AdminIntegrationClientRouteRequest
{
    public required Guid TenantId { get; init; }

    public required string ClientId { get; init; }
}

public sealed record AdminIntegrationClientUpdateScopesRequest
{
    public required Guid TenantId { get; init; }

    public required string ClientId { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();
}

public sealed record AdminIntegrationClientView
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required AdminIntegrationClientStatus Status { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }

    public DateTimeOffset? LastSecretRotatedUtc { get; init; }

    public required DateTimeOffset LastAuthStateChangedUtc { get; init; }
}

public enum AdminListIntegrationClientsErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
}

public sealed record AdminListIntegrationClientsResult
{
    public bool IsSuccess { get; init; }

    public AdminListIntegrationClientsErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyCollection<AdminIntegrationClientView> Clients { get; init; } = Array.Empty<AdminIntegrationClientView>();

    public static AdminListIntegrationClientsResult Success(IReadOnlyCollection<AdminIntegrationClientView> clients) => new()
    {
        IsSuccess = true,
        Clients = clients,
    };

    public static AdminListIntegrationClientsResult Failure(
        AdminListIntegrationClientsErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminCreateIntegrationClientErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    Conflict = 2,
}

public sealed record AdminCreateIntegrationClientResult
{
    public bool IsSuccess { get; init; }

    public AdminCreateIntegrationClientErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminIntegrationClientView? Client { get; init; }

    public string? ClientSecret { get; init; }

    public static AdminCreateIntegrationClientResult Success(
        AdminIntegrationClientView client,
        string clientSecret) => new()
    {
        IsSuccess = true,
        Client = client,
        ClientSecret = clientSecret,
    };

    public static AdminCreateIntegrationClientResult Failure(
        AdminCreateIntegrationClientErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminRotateIntegrationClientSecretErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
}

public sealed record AdminRotateIntegrationClientSecretResult
{
    public bool IsSuccess { get; init; }

    public AdminRotateIntegrationClientSecretErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminIntegrationClientView? Client { get; init; }

    public string? ClientSecret { get; init; }

    public static AdminRotateIntegrationClientSecretResult Success(
        AdminIntegrationClientView client,
        string clientSecret) => new()
    {
        IsSuccess = true,
        Client = client,
        ClientSecret = clientSecret,
    };

    public static AdminRotateIntegrationClientSecretResult Failure(
        AdminRotateIntegrationClientSecretErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminUpdateIntegrationClientScopesErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
}

public sealed record AdminUpdateIntegrationClientScopesResult
{
    public bool IsSuccess { get; init; }

    public AdminUpdateIntegrationClientScopesErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminIntegrationClientView? Client { get; init; }

    public static AdminUpdateIntegrationClientScopesResult Success(AdminIntegrationClientView client) => new()
    {
        IsSuccess = true,
        Client = client,
    };

    public static AdminUpdateIntegrationClientScopesResult Failure(
        AdminUpdateIntegrationClientScopesErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminSetIntegrationClientActiveStateErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
    Conflict = 3,
}

public sealed record AdminSetIntegrationClientActiveStateResult
{
    public bool IsSuccess { get; init; }

    public AdminSetIntegrationClientActiveStateErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminIntegrationClientView? Client { get; init; }

    public static AdminSetIntegrationClientActiveStateResult Success(AdminIntegrationClientView client) => new()
    {
        IsSuccess = true,
        Client = client,
    };

    public static AdminSetIntegrationClientActiveStateResult Failure(
        AdminSetIntegrationClientActiveStateErrorCode errorCode,
        string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public sealed record AdminIntegrationClientCreateDraft
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ClientSecretHash { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();

    public required DateTimeOffset CreatedUtc { get; init; }
}

public interface IAdminIntegrationClientStore
{
    Task<IReadOnlyCollection<AdminIntegrationClientView>> ListByTenantAsync(
        AdminIntegrationClientListRequest request,
        CancellationToken cancellationToken);

    Task<AdminIntegrationClientView?> GetByTenantAndClientIdAsync(
        Guid tenantId,
        string clientId,
        CancellationToken cancellationToken);

    Task<AdminIntegrationClientView?> CreateAsync(
        AdminIntegrationClientCreateDraft draft,
        CancellationToken cancellationToken);

    Task<AdminIntegrationClientView?> RotateSecretAsync(
        Guid tenantId,
        string clientId,
        string clientSecretHash,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);

    Task<AdminIntegrationClientView?> UpdateScopesAsync(
        Guid tenantId,
        string clientId,
        IReadOnlyCollection<string> allowedScopes,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);

    Task<AdminIntegrationClientView?> SetIsActiveAsync(
        Guid tenantId,
        string clientId,
        bool isActive,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);
}
