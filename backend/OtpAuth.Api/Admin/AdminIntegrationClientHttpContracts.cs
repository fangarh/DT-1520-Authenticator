using System.Text.Json;
using System.Text.Json.Serialization;

namespace OtpAuth.Api.Admin;

public sealed record AdminCreateIntegrationClientHttpRequest
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed record AdminIntegrationClientHttpResponse
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string Status { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }

    public DateTimeOffset? LastSecretRotatedUtc { get; init; }

    public required DateTimeOffset LastAuthStateChangedUtc { get; init; }
}

public sealed record AdminCreateIntegrationClientHttpResponse
{
    public required AdminIntegrationClientHttpResponse Client { get; init; }

    public required string ClientSecret { get; init; }
}

public sealed record AdminRotateIntegrationClientSecretHttpResponse
{
    public required AdminIntegrationClientHttpResponse Client { get; init; }

    public required string ClientSecret { get; init; }
}

public sealed record AdminUpdateIntegrationClientScopesHttpRequest
{
    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();
}
