using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class InMemoryAdminLoginRateLimiterTests
{
    [Fact]
    public async Task RegisterFailureAsync_BlocksAfterConfiguredThreshold()
    {
        var limiter = new InMemoryAdminLoginRateLimiter();
        var key = new AdminLoginAttemptKey
        {
            NormalizedUsername = "OPERATOR",
            RemoteAddress = "127.0.0.1",
        };
        var now = DateTimeOffset.UtcNow;
        AdminLoginRateLimitDecision decision = new();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            decision = await limiter.RegisterFailureAsync(key, now.AddSeconds(attempt), CancellationToken.None);
        }

        Assert.True(decision.IsRateLimited);
        Assert.NotNull(decision.RetryAfterSeconds);
    }

    [Fact]
    public async Task ResetAsync_ClearsExistingFailureWindow()
    {
        var limiter = new InMemoryAdminLoginRateLimiter();
        var key = new AdminLoginAttemptKey
        {
            NormalizedUsername = "OPERATOR",
            RemoteAddress = "127.0.0.1",
        };
        var now = DateTimeOffset.UtcNow;

        _ = await limiter.RegisterFailureAsync(key, now, CancellationToken.None);
        await limiter.ResetAsync(key, CancellationToken.None);
        var decision = await limiter.GetStatusAsync(key, now.AddSeconds(1), CancellationToken.None);

        Assert.False(decision.IsRateLimited);
    }
}
