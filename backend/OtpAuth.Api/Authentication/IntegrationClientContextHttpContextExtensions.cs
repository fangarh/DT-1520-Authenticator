using System.Security.Claims;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Api.Authentication;

public static class IntegrationClientContextHttpContextExtensions
{
    public static IntegrationClientContext GetRequiredIntegrationClientContext(this HttpContext httpContext)
    {
        var principal = httpContext.User;
        var clientId = principal.FindFirstValue("client_id");
        var tenantId = principal.FindFirstValue("tenant_id");
        var applicationClientId = principal.FindFirstValue("application_client_id");
        var scopeValue = principal.FindFirstValue("scope");

        if (string.IsNullOrWhiteSpace(clientId) ||
            !Guid.TryParse(tenantId, out var parsedTenantId) ||
            !Guid.TryParse(applicationClientId, out var parsedApplicationClientId))
        {
            throw new InvalidOperationException("Authenticated principal is missing integration client claims.");
        }

        var scopes = string.IsNullOrWhiteSpace(scopeValue)
            ? Array.Empty<string>()
            : scopeValue
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        return new IntegrationClientContext
        {
            ClientId = clientId,
            TenantId = parsedTenantId,
            ApplicationClientId = parsedApplicationClientId,
            Scopes = scopes,
        };
    }
}
