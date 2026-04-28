using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Admin;

public static class AdminIntegrationClientRequestMapper
{
    private static readonly string[] SecretBearingPropertyNames =
    [
        "clientSecret",
        "client_secret",
        "clientSecretHash",
        "client_secret_hash",
        "secret",
    ];

    public static AdminIntegrationClientCreateRequest Map(AdminCreateIntegrationClientHttpRequest request)
    {
        return new AdminIntegrationClientCreateRequest
        {
            ClientId = request.ClientId,
            TenantId = request.TenantId,
            ApplicationClientId = request.ApplicationClientId,
            AllowedScopes = request.AllowedScopes,
            HasOperatorProvidedSecret = ContainsSecretBearingProperty(request),
        };
    }

    public static AdminIntegrationClientUpdateScopesRequest Map(
        Guid tenantId,
        string clientId,
        AdminUpdateIntegrationClientScopesHttpRequest request)
    {
        return new AdminIntegrationClientUpdateScopesRequest
        {
            TenantId = tenantId,
            ClientId = clientId,
            AllowedScopes = request.AllowedScopes,
        };
    }

    public static AdminIntegrationClientHttpResponse MapResponse(AdminIntegrationClientView client)
    {
        return new AdminIntegrationClientHttpResponse
        {
            ClientId = client.ClientId,
            TenantId = client.TenantId,
            ApplicationClientId = client.ApplicationClientId,
            Status = ToHttpValue(client.Status),
            AllowedScopes = client.AllowedScopes
                .OrderBy(static scope => scope, StringComparer.Ordinal)
                .ToArray(),
            CreatedUtc = client.CreatedUtc,
            UpdatedUtc = client.UpdatedUtc,
            LastSecretRotatedUtc = client.LastSecretRotatedUtc,
            LastAuthStateChangedUtc = client.LastAuthStateChangedUtc,
        };
    }

    public static AdminCreateIntegrationClientHttpResponse MapCreateResponse(
        AdminIntegrationClientView client,
        string clientSecret)
    {
        return new AdminCreateIntegrationClientHttpResponse
        {
            Client = MapResponse(client),
            ClientSecret = clientSecret,
        };
    }

    public static AdminRotateIntegrationClientSecretHttpResponse MapRotateSecretResponse(
        AdminIntegrationClientView client,
        string clientSecret)
    {
        return new AdminRotateIntegrationClientSecretHttpResponse
        {
            Client = MapResponse(client),
            ClientSecret = clientSecret,
        };
    }

    private static bool ContainsSecretBearingProperty(AdminCreateIntegrationClientHttpRequest request)
    {
        return request.AdditionalProperties is not null &&
               request.AdditionalProperties.Keys.Any(key =>
                   SecretBearingPropertyNames.Contains(key, StringComparer.OrdinalIgnoreCase));
    }

    private static string ToHttpValue(AdminIntegrationClientStatus status)
    {
        return status switch
        {
            AdminIntegrationClientStatus.Active => "active",
            AdminIntegrationClientStatus.Inactive => "inactive",
            _ => throw new InvalidOperationException($"Unsupported integration client status '{status}'."),
        };
    }
}
