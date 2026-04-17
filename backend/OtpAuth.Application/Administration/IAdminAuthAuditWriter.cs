namespace OtpAuth.Application.Administration;

public interface IAdminAuthAuditWriter
{
    Task WriteLoginSucceededAsync(
        AdminAuthenticatedUser user,
        string? remoteAddress,
        CancellationToken cancellationToken);

    Task WriteLoginFailedAsync(
        string normalizedUsername,
        string? remoteAddress,
        Guid? adminUserId,
        bool isRateLimited,
        int? retryAfterSeconds,
        CancellationToken cancellationToken);

    Task WriteLogoutAsync(
        AdminAuthenticatedUser user,
        string? remoteAddress,
        CancellationToken cancellationToken);
}
