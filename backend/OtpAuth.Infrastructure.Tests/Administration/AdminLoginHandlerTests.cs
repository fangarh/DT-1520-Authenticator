using OtpAuth.Application.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminLoginHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsAuthenticatedUser_WhenCredentialsAreValid()
    {
        var auditWriter = new RecordingAuditWriter();
        var rateLimiter = new StubRateLimiter();
        var handler = new AdminLoginHandler(
            new InMemoryAdminUserStore(new AdminUser
            {
                AdminUserId = Guid.Parse("3a42de02-727e-4f96-83c4-858f5ea8b2c5"),
                Username = "operator",
                NormalizedUsername = "OPERATOR",
                PasswordHash = "valid-hash",
                IsActive = true,
                Permissions = [AdminPermissions.EnrollmentsRead, AdminPermissions.EnrollmentsWrite],
            }),
            new StubPasswordHasher(isValid: true),
            rateLimiter,
            auditWriter);

        var result = await handler.HandleAsync(
            new AdminLoginRequest
            {
                Username = " operator ",
                Password = "secret",
                RemoteAddress = "10.0.0.1",
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.User);
        Assert.Equal("operator", result.User!.Username);
        Assert.Equal(2, result.User.Permissions.Count);
        Assert.Equal("OPERATOR|10.0.0.1", rateLimiter.LastResetKey);
        Assert.Single(auditWriter.Successes);
        Assert.Empty(auditWriter.Failures);
    }

    [Fact]
    public async Task HandleAsync_ReturnsInvalidCredentials_WhenPasswordIsWrong()
    {
        var auditWriter = new RecordingAuditWriter();
        var rateLimiter = new StubRateLimiter();
        var handler = new AdminLoginHandler(
            new InMemoryAdminUserStore(new AdminUser
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                NormalizedUsername = "OPERATOR",
                PasswordHash = "valid-hash",
                IsActive = true,
            }),
            new StubPasswordHasher(isValid: false),
            rateLimiter,
            auditWriter);

        var result = await handler.HandleAsync(
            new AdminLoginRequest
            {
                Username = "operator",
                Password = "wrong",
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminLoginErrorCode.InvalidCredentials, result.ErrorCode);
        Assert.Single(auditWriter.Failures);
        Assert.False(auditWriter.Failures[0].IsRateLimited);
        Assert.Equal("OPERATOR|unknown", rateLimiter.LastFailureKey);
    }

    [Fact]
    public async Task HandleAsync_ReturnsRateLimited_WhenLimiterBlocksRequest()
    {
        var auditWriter = new RecordingAuditWriter();
        var rateLimiter = new StubRateLimiter
        {
            CurrentStatus = new AdminLoginRateLimitDecision
            {
                IsRateLimited = true,
                RetryAfterSeconds = 30,
            },
        };
        var handler = new AdminLoginHandler(
            new InMemoryAdminUserStore(),
            new StubPasswordHasher(isValid: false),
            rateLimiter,
            auditWriter);

        var result = await handler.HandleAsync(
            new AdminLoginRequest
            {
                Username = "operator",
                Password = "secret",
                RemoteAddress = "127.0.0.1",
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminLoginErrorCode.RateLimited, result.ErrorCode);
        Assert.Equal(30, result.RetryAfterSeconds);
        Assert.Single(auditWriter.Failures);
        Assert.True(auditWriter.Failures[0].IsRateLimited);
        Assert.Null(rateLimiter.LastFailureKey);
    }

    private sealed class InMemoryAdminUserStore : IAdminUserStore
    {
        private readonly IReadOnlyDictionary<string, AdminUser> _users;

        public InMemoryAdminUserStore(params AdminUser[] users)
        {
            _users = users.ToDictionary(user => user.NormalizedUsername, StringComparer.Ordinal);
        }

        public Task<AdminUser?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _users.TryGetValue(normalizedUsername, out var user);
            return Task.FromResult(user);
        }
    }

    private sealed class StubPasswordHasher : IAdminPasswordHasher
    {
        private readonly bool _isValid;

        public StubPasswordHasher(bool isValid)
        {
            _isValid = isValid;
        }

        public string Hash(string password)
        {
            return "hash";
        }

        public bool Verify(string password, string passwordHash)
        {
            return _isValid;
        }
    }

    private sealed class StubRateLimiter : IAdminLoginRateLimiter
    {
        public AdminLoginRateLimitDecision CurrentStatus { get; set; } = new();
        public AdminLoginRateLimitDecision FailureStatus { get; set; } = new();
        public string? LastFailureKey { get; private set; }
        public string? LastResetKey { get; private set; }

        public Task<AdminLoginRateLimitDecision> GetStatusAsync(AdminLoginAttemptKey key, DateTimeOffset now, CancellationToken cancellationToken)
        {
            return Task.FromResult(CurrentStatus);
        }

        public Task<AdminLoginRateLimitDecision> RegisterFailureAsync(AdminLoginAttemptKey key, DateTimeOffset now, CancellationToken cancellationToken)
        {
            LastFailureKey = $"{key.NormalizedUsername}|{key.RemoteAddress}";
            return Task.FromResult(FailureStatus);
        }

        public Task ResetAsync(AdminLoginAttemptKey key, CancellationToken cancellationToken)
        {
            LastResetKey = $"{key.NormalizedUsername}|{key.RemoteAddress}";
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuditWriter : IAdminAuthAuditWriter
    {
        public List<(string Username, bool IsRateLimited)> Failures { get; } = [];
        public List<string> Successes { get; } = [];

        public Task WriteLoginSucceededAsync(AdminAuthenticatedUser user, string? remoteAddress, CancellationToken cancellationToken)
        {
            Successes.Add(user.Username);
            return Task.CompletedTask;
        }

        public Task WriteLoginFailedAsync(string normalizedUsername, string? remoteAddress, Guid? adminUserId, bool isRateLimited, int? retryAfterSeconds, CancellationToken cancellationToken)
        {
            Failures.Add((normalizedUsername, isRateLimited));
            return Task.CompletedTask;
        }

        public Task WriteLogoutAsync(AdminAuthenticatedUser user, string? remoteAddress, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
