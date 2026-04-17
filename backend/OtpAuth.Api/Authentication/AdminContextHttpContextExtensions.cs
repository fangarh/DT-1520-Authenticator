using System.Security.Claims;
using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Authentication;

public static class AdminContextHttpContextExtensions
{
    public static AdminContext GetRequiredAdminContext(this HttpContext httpContext)
    {
        var principal = httpContext.User;
        var adminUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = principal.FindFirstValue(ClaimTypes.Name);
        if (!Guid.TryParse(adminUserId, out var parsedAdminUserId) ||
            string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("Authenticated principal is missing admin session claims.");
        }

        var permissions = principal.FindAll(AdminClaimTypes.Permission)
            .Select(static claim => claim.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new AdminContext
        {
            AdminUserId = parsedAdminUserId,
            Username = username,
            Permissions = permissions,
        };
    }
}
