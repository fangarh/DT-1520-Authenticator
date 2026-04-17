namespace OtpAuth.Application.Administration;

public sealed class AdminLoginHandler
{
    private readonly IAdminUserStore _userStore;
    private readonly IAdminPasswordHasher _passwordHasher;
    private readonly IAdminLoginRateLimiter _rateLimiter;
    private readonly IAdminAuthAuditWriter _auditWriter;

    public AdminLoginHandler(
        IAdminUserStore userStore,
        IAdminPasswordHasher passwordHasher,
        IAdminLoginRateLimiter rateLimiter,
        IAdminAuthAuditWriter auditWriter)
    {
        _userStore = userStore;
        _passwordHasher = passwordHasher;
        _rateLimiter = rateLimiter;
        _auditWriter = auditWriter;
    }

    public async Task<AdminLoginResult> HandleAsync(
        AdminLoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(request.Username);
        var normalizedRemoteAddress = NormalizeRemoteAddress(request.RemoteAddress);
        if (normalizedUsername is null)
        {
            return AdminLoginResult.Failure(
                AdminLoginErrorCode.ValidationFailed,
                "Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return AdminLoginResult.Failure(
                AdminLoginErrorCode.ValidationFailed,
                "Password is required.");
        }

        var attemptKey = new AdminLoginAttemptKey
        {
            NormalizedUsername = normalizedUsername,
            RemoteAddress = normalizedRemoteAddress ?? "unknown",
        };

        var now = DateTimeOffset.UtcNow;
        var currentRateLimit = await _rateLimiter.GetStatusAsync(attemptKey, now, cancellationToken);
        if (currentRateLimit.IsRateLimited)
        {
            await _auditWriter.WriteLoginFailedAsync(
                normalizedUsername,
                normalizedRemoteAddress,
                adminUserId: null,
                isRateLimited: true,
                currentRateLimit.RetryAfterSeconds,
                cancellationToken);
            return AdminLoginResult.Failure(
                AdminLoginErrorCode.RateLimited,
                "Too many failed login attempts. Retry later.",
                currentRateLimit.RetryAfterSeconds);
        }

        var user = await _userStore.GetByNormalizedUsernameAsync(normalizedUsername, cancellationToken);
        var isPasswordValid =
            user is not null &&
            user.IsActive &&
            _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            var updatedRateLimit = await _rateLimiter.RegisterFailureAsync(attemptKey, now, cancellationToken);
            await _auditWriter.WriteLoginFailedAsync(
                normalizedUsername,
                normalizedRemoteAddress,
                user?.AdminUserId,
                updatedRateLimit.IsRateLimited,
                updatedRateLimit.RetryAfterSeconds,
                cancellationToken);
            return updatedRateLimit.IsRateLimited
                ? AdminLoginResult.Failure(
                    AdminLoginErrorCode.RateLimited,
                    "Too many failed login attempts. Retry later.",
                    updatedRateLimit.RetryAfterSeconds)
                : AdminLoginResult.Failure(
                    AdminLoginErrorCode.InvalidCredentials,
                    "Invalid username or password.");
        }

        await _rateLimiter.ResetAsync(attemptKey, cancellationToken);
        var authenticatedAdmin = user!;

        var authenticatedUser = new AdminAuthenticatedUser
        {
            AdminUserId = authenticatedAdmin.AdminUserId,
            Username = authenticatedAdmin.Username,
            Permissions = authenticatedAdmin.Permissions
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };

        await _auditWriter.WriteLoginSucceededAsync(
            authenticatedUser,
            normalizedRemoteAddress,
            cancellationToken);

        return AdminLoginResult.Success(authenticatedUser);
    }

    private static string? NormalizeUsername(string? username)
    {
        return string.IsNullOrWhiteSpace(username)
            ? null
            : username.Trim().ToUpperInvariant();
    }

    private static string? NormalizeRemoteAddress(string? remoteAddress)
    {
        return string.IsNullOrWhiteSpace(remoteAddress)
            ? null
            : remoteAddress.Trim();
    }
}
