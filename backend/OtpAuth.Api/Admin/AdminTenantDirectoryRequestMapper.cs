using System.Text.Json;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Api.Admin;

public static class AdminTenantDirectoryRequestMapper
{
    private static readonly string[] SecretBearingPropertyNames =
    [
        "clientSecret",
        "client_secret",
        "clientSecretHash",
        "client_secret_hash",
        "secret",
    ];

    public static AdminTenantCreateRequest Map(AdminCreateTenantHttpRequest request)
    {
        return new AdminTenantCreateRequest
        {
            TenantId = request.TenantId,
            DisplayName = request.DisplayName,
            Slug = request.Slug,
            Status = ParseStatusOrDefault(request.Status),
        };
    }

    public static AdminTenantQuickCreateRequest Map(AdminQuickCreateTenantHttpRequest request)
    {
        return new AdminTenantQuickCreateRequest
        {
            TenantDisplayName = request.TenantDisplayName,
            ApplicationDisplayName = request.ApplicationDisplayName,
            IntegrationClientDisplayName = request.IntegrationClientDisplayName,
            AllowedScopes = request.AllowedScopes.Count == 0
                ?
                [
                    IntegrationClientScopes.ChallengesRead,
                    IntegrationClientScopes.ChallengesWrite,
                    IntegrationClientScopes.DevicesWrite,
                    IntegrationClientScopes.EnrollmentsWrite,
                ]
                : request.AllowedScopes,
            HasOperatorProvidedSecret = ContainsSecretBearingProperty(request.AdditionalProperties),
        };
    }

    public static AdminTenantDirectoryTenantHttpResponse MapTenant(AdminTenantDirectoryTenantView tenant)
    {
        return new AdminTenantDirectoryTenantHttpResponse
        {
            TenantId = tenant.TenantId,
            DisplayName = tenant.DisplayName,
            Slug = tenant.Slug,
            Status = ToHttpStatus(tenant.Status),
            ApplicationCount = tenant.ApplicationCount,
            IntegrationClientCount = tenant.IntegrationClientCount,
            CreatedUtc = tenant.CreatedUtc,
            UpdatedUtc = tenant.UpdatedUtc,
        };
    }

    public static AdminTenantDirectoryDetailHttpResponse MapDirectory(AdminTenantDirectoryDetailView directory)
    {
        return new AdminTenantDirectoryDetailHttpResponse
        {
            Tenant = MapTenant(directory.Tenant),
            Applications = directory.Applications
                .Select(MapApplication)
                .ToArray(),
            IntegrationClients = directory.IntegrationClients
                .Select(AdminIntegrationClientRequestMapper.MapResponse)
                .ToArray(),
        };
    }

    public static AdminQuickCreateTenantHttpResponse MapQuickCreate(
        AdminTenantDirectoryDetailView directory,
        AdminIntegrationClientView client,
        string clientSecret)
    {
        return new AdminQuickCreateTenantHttpResponse
        {
            Directory = MapDirectory(directory),
            Client = AdminIntegrationClientRequestMapper.MapResponse(client),
            ClientSecret = clientSecret,
        };
    }

    private static AdminTenantDirectoryApplicationHttpResponse MapApplication(AdminTenantDirectoryApplicationView application)
    {
        return new AdminTenantDirectoryApplicationHttpResponse
        {
            ApplicationClientId = application.ApplicationClientId,
            TenantId = application.TenantId,
            DisplayName = application.DisplayName,
            Slug = application.Slug,
            Status = ToHttpStatus(application.Status),
            IntegrationClientCount = application.IntegrationClientCount,
            CreatedUtc = application.CreatedUtc,
            UpdatedUtc = application.UpdatedUtc,
        };
    }

    private static AdminTenantDirectoryStatus ParseStatusOrDefault(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return AdminTenantDirectoryStatus.Active;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "active" => AdminTenantDirectoryStatus.Active,
            "disabled" => AdminTenantDirectoryStatus.Disabled,
            "archived" => AdminTenantDirectoryStatus.Archived,
            "test" => AdminTenantDirectoryStatus.Test,
            _ => throw new InvalidOperationException($"Unsupported tenant status '{status}'."),
        };
    }

    private static string ToHttpStatus(AdminTenantDirectoryStatus status)
    {
        return status switch
        {
            AdminTenantDirectoryStatus.Active => "active",
            AdminTenantDirectoryStatus.Disabled => "disabled",
            AdminTenantDirectoryStatus.Archived => "archived",
            AdminTenantDirectoryStatus.Test => "test",
            _ => throw new InvalidOperationException($"Unsupported tenant status '{status}'."),
        };
    }

    private static bool ContainsSecretBearingProperty(IDictionary<string, JsonElement>? additionalProperties)
    {
        return additionalProperties is not null &&
               additionalProperties.Keys.Any(key =>
                   SecretBearingPropertyNames.Contains(key, StringComparer.OrdinalIgnoreCase));
    }
}
