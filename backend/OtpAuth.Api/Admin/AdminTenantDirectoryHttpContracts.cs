using System.Text.Json;
using System.Text.Json.Serialization;

namespace OtpAuth.Api.Admin;

public sealed record AdminCreateTenantHttpRequest
{
    public Guid? TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? Slug { get; init; }

    public string? Status { get; init; }
}

public sealed record AdminQuickCreateTenantHttpRequest
{
    public required string TenantDisplayName { get; init; }

    public required string ApplicationDisplayName { get; init; }

    public required string IntegrationClientDisplayName { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed record AdminTenantDirectoryTenantHttpResponse
{
    public required Guid TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? Slug { get; init; }

    public required string Status { get; init; }

    public required int ApplicationCount { get; init; }

    public required int IntegrationClientCount { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }
}

public sealed record AdminTenantDirectoryApplicationHttpResponse
{
    public required Guid ApplicationClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? Slug { get; init; }

    public required string Status { get; init; }

    public required int IntegrationClientCount { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }
}

public sealed record AdminTenantDirectoryDetailHttpResponse
{
    public required AdminTenantDirectoryTenantHttpResponse Tenant { get; init; }

    public IReadOnlyCollection<AdminTenantDirectoryApplicationHttpResponse> Applications { get; init; } = Array.Empty<AdminTenantDirectoryApplicationHttpResponse>();

    public IReadOnlyCollection<AdminIntegrationClientHttpResponse> IntegrationClients { get; init; } = Array.Empty<AdminIntegrationClientHttpResponse>();
}

public sealed record AdminQuickCreateTenantHttpResponse
{
    public required AdminTenantDirectoryDetailHttpResponse Directory { get; init; }

    public required AdminIntegrationClientHttpResponse Client { get; init; }

    public required string ClientSecret { get; init; }
}
