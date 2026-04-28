using OtpAuth.Application.Integrations;

namespace OtpAuth.Application.Administration;

public enum AdminTenantDirectoryStatus
{
    Active = 0,
    Disabled = 1,
    Archived = 2,
    Test = 3,
}

public sealed record AdminTenantDirectoryListRequest;

public sealed record AdminTenantDirectoryDetailRequest
{
    public required Guid TenantId { get; init; }
}

public sealed record AdminTenantCreateRequest
{
    public Guid? TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? Slug { get; init; }

    public AdminTenantDirectoryStatus Status { get; init; } = AdminTenantDirectoryStatus.Active;
}

public sealed record AdminTenantQuickCreateRequest
{
    public required string TenantDisplayName { get; init; }

    public required string ApplicationDisplayName { get; init; }

    public required string IntegrationClientDisplayName { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } =
    [
        IntegrationClientScopes.ChallengesRead,
        IntegrationClientScopes.ChallengesWrite,
        IntegrationClientScopes.DevicesWrite,
        IntegrationClientScopes.EnrollmentsWrite,
    ];

    public bool HasOperatorProvidedSecret { get; init; }
}

public sealed record AdminTenantDirectoryTenantView
{
    public required Guid TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? Slug { get; init; }

    public required AdminTenantDirectoryStatus Status { get; init; }

    public required int ApplicationCount { get; init; }

    public required int IntegrationClientCount { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }
}

public sealed record AdminTenantDirectoryApplicationView
{
    public required Guid ApplicationClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? Slug { get; init; }

    public required AdminTenantDirectoryStatus Status { get; init; }

    public required int IntegrationClientCount { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }
}

public sealed record AdminTenantDirectoryDetailView
{
    public required AdminTenantDirectoryTenantView Tenant { get; init; }

    public IReadOnlyCollection<AdminTenantDirectoryApplicationView> Applications { get; init; } = Array.Empty<AdminTenantDirectoryApplicationView>();

    public IReadOnlyCollection<AdminIntegrationClientView> IntegrationClients { get; init; } = Array.Empty<AdminIntegrationClientView>();
}

public sealed record AdminTenantQuickCreateDraft
{
    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string TenantDisplayName { get; init; }

    public string? TenantSlug { get; init; }

    public required string ApplicationDisplayName { get; init; }

    public string? ApplicationSlug { get; init; }

    public required string ClientId { get; init; }

    public required string ClientSecretHash { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();

    public required DateTimeOffset CreatedUtc { get; init; }
}

public sealed record AdminTenantCreateDraft
{
    public required Guid TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? Slug { get; init; }

    public required AdminTenantDirectoryStatus Status { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

public enum AdminListTenantDirectoryErrorCode
{
    AccessDenied = 0,
}

public sealed record AdminListTenantDirectoryResult
{
    public bool IsSuccess { get; init; }

    public AdminListTenantDirectoryErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyCollection<AdminTenantDirectoryTenantView> Tenants { get; init; } = Array.Empty<AdminTenantDirectoryTenantView>();

    public static AdminListTenantDirectoryResult Success(IReadOnlyCollection<AdminTenantDirectoryTenantView> tenants) => new()
    {
        IsSuccess = true,
        Tenants = tenants,
    };

    public static AdminListTenantDirectoryResult Failure(AdminListTenantDirectoryErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminGetTenantDirectoryErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    NotFound = 2,
}

public sealed record AdminGetTenantDirectoryResult
{
    public bool IsSuccess { get; init; }

    public AdminGetTenantDirectoryErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminTenantDirectoryDetailView? Directory { get; init; }

    public static AdminGetTenantDirectoryResult Success(AdminTenantDirectoryDetailView directory) => new()
    {
        IsSuccess = true,
        Directory = directory,
    };

    public static AdminGetTenantDirectoryResult Failure(AdminGetTenantDirectoryErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminCreateTenantErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    Conflict = 2,
}

public sealed record AdminCreateTenantResult
{
    public bool IsSuccess { get; init; }

    public AdminCreateTenantErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminTenantDirectoryTenantView? Tenant { get; init; }

    public static AdminCreateTenantResult Success(AdminTenantDirectoryTenantView tenant) => new()
    {
        IsSuccess = true,
        Tenant = tenant,
    };

    public static AdminCreateTenantResult Failure(AdminCreateTenantErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public enum AdminQuickCreateTenantErrorCode
{
    ValidationFailed = 0,
    AccessDenied = 1,
    Conflict = 2,
}

public sealed record AdminQuickCreateTenantResult
{
    public bool IsSuccess { get; init; }

    public AdminQuickCreateTenantErrorCode? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public AdminTenantDirectoryDetailView? Directory { get; init; }

    public AdminIntegrationClientView? Client { get; init; }

    public string? ClientSecret { get; init; }

    public static AdminQuickCreateTenantResult Success(
        AdminTenantDirectoryDetailView directory,
        AdminIntegrationClientView client,
        string clientSecret) => new()
    {
        IsSuccess = true,
        Directory = directory,
        Client = client,
        ClientSecret = clientSecret,
    };

    public static AdminQuickCreateTenantResult Failure(AdminQuickCreateTenantErrorCode errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public interface IAdminTenantDirectoryStore
{
    Task<IReadOnlyCollection<AdminTenantDirectoryTenantView>> ListTenantsAsync(CancellationToken cancellationToken);

    Task<AdminTenantDirectoryDetailView?> GetDirectoryAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<AdminTenantDirectoryTenantView?> CreateTenantAsync(AdminTenantCreateDraft draft, CancellationToken cancellationToken);

    Task<AdminTenantDirectoryDetailView?> QuickCreateAsync(AdminTenantQuickCreateDraft draft, CancellationToken cancellationToken);
}
