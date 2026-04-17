using System.Security.Claims;
using OtpAuth.Application.Devices;

namespace OtpAuth.Api.Authentication;

public static class DeviceClientContextHttpContextExtensions
{
    public static DeviceClientContext GetRequiredDeviceClientContext(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var principal = httpContext.User;
        var deviceId = principal.FindFirstValue("device_id");
        var tenantId = principal.FindFirstValue("tenant_id");
        var applicationClientId = principal.FindFirstValue("application_client_id");
        var scopeValue = principal.FindFirstValue("scope");

        if (!Guid.TryParse(deviceId, out var parsedDeviceId) ||
            !Guid.TryParse(tenantId, out var parsedTenantId) ||
            !Guid.TryParse(applicationClientId, out var parsedApplicationClientId))
        {
            throw new InvalidOperationException("Authenticated principal is missing device claims.");
        }

        var scopes = string.IsNullOrWhiteSpace(scopeValue)
            ? Array.Empty<string>()
            : scopeValue
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        return new DeviceClientContext
        {
            DeviceId = parsedDeviceId,
            TenantId = parsedTenantId,
            ApplicationClientId = parsedApplicationClientId,
            Scopes = scopes,
        };
    }
}
