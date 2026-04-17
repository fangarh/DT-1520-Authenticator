namespace OtpAuth.Application.Administration;

public interface IAdminLoginRateLimiter
{
    Task<AdminLoginRateLimitDecision> GetStatusAsync(
        AdminLoginAttemptKey key,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<AdminLoginRateLimitDecision> RegisterFailureAsync(
        AdminLoginAttemptKey key,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task ResetAsync(AdminLoginAttemptKey key, CancellationToken cancellationToken);
}
