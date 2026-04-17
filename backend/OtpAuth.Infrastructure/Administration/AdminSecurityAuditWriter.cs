using System.Text.Json;
using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Security;

namespace OtpAuth.Infrastructure.Administration;

public sealed class AdminSecurityAuditWriter : IAdminAuthAuditWriter
{
    private readonly SecurityAuditService _securityAuditService;

    public AdminSecurityAuditWriter(SecurityAuditService securityAuditService)
    {
        _securityAuditService = securityAuditService;
    }

    public Task WriteLoginSucceededAsync(
        AdminAuthenticatedUser user,
        string? remoteAddress,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "admin_auth.login_succeeded",
                SubjectType = "admin_user",
                SubjectId = user.AdminUserId.ToString(),
                Summary = $"Admin user '{user.Username}' signed in.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId = user.AdminUserId,
                    username = user.Username,
                    permissions = user.Permissions.OrderBy(static permission => permission, StringComparer.Ordinal),
                    remoteAddress = NormalizeOptional(remoteAddress),
                }),
                Severity = "info",
                Source = "admin_api",
            },
            cancellationToken);
    }

    public Task WriteLoginFailedAsync(
        string normalizedUsername,
        string? remoteAddress,
        Guid? adminUserId,
        bool isRateLimited,
        int? retryAfterSeconds,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = isRateLimited
                    ? "admin_auth.login_rate_limited"
                    : "admin_auth.login_failed",
                SubjectType = adminUserId is null
                    ? "admin_login_attempt"
                    : "admin_user",
                SubjectId = adminUserId?.ToString() ?? normalizedUsername,
                Summary = isRateLimited
                    ? $"Admin login rate limit triggered for '{normalizedUsername}'."
                    : $"Admin login failed for '{normalizedUsername}'.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId,
                    normalizedUsername,
                    remoteAddress = NormalizeOptional(remoteAddress),
                    retryAfterSeconds,
                }),
                Severity = "warning",
                Source = "admin_api",
            },
            cancellationToken);
    }

    public Task WriteLogoutAsync(
        AdminAuthenticatedUser user,
        string? remoteAddress,
        CancellationToken cancellationToken)
    {
        return _securityAuditService.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = "admin_auth.logout_succeeded",
                SubjectType = "admin_user",
                SubjectId = user.AdminUserId.ToString(),
                Summary = $"Admin user '{user.Username}' signed out.",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    adminUserId = user.AdminUserId,
                    username = user.Username,
                    remoteAddress = NormalizeOptional(remoteAddress),
                }),
                Severity = "info",
                Source = "admin_api",
            },
            cancellationToken);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
