using System.Collections.Concurrent;
using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

public sealed class InMemoryAdminLoginRateLimiter : IAdminLoginRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private const int MaxFailedAttempts = 5;

    private readonly ConcurrentDictionary<string, FailureState> _failures = new(StringComparer.Ordinal);

    public Task<AdminLoginRateLimitDecision> GetStatusAsync(
        AdminLoginAttemptKey key,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cacheKey = ToCacheKey(key);
        if (!_failures.TryGetValue(cacheKey, out var state))
        {
            return Task.FromResult(new AdminLoginRateLimitDecision());
        }

        if (state.BlockedUntilUtc is not null && state.BlockedUntilUtc > now)
        {
            return Task.FromResult(new AdminLoginRateLimitDecision
            {
                IsRateLimited = true,
                RetryAfterSeconds = Math.Max(1, (int)Math.Ceiling((state.BlockedUntilUtc.Value - now).TotalSeconds)),
            });
        }

        if (state.WindowStartedUtc + Window <= now)
        {
            _failures.TryRemove(cacheKey, out _);
        }

        return Task.FromResult(new AdminLoginRateLimitDecision());
    }

    public Task<AdminLoginRateLimitDecision> RegisterFailureAsync(
        AdminLoginAttemptKey key,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheKey = ToCacheKey(key);
        var state = _failures.AddOrUpdate(
            cacheKey,
            _ => new FailureState
            {
                WindowStartedUtc = now,
                FailedAttempts = 1,
            },
            (_, existing) => UpdateState(existing, now));

        if (state.BlockedUntilUtc is null || state.BlockedUntilUtc <= now)
        {
            return Task.FromResult(new AdminLoginRateLimitDecision());
        }

        return Task.FromResult(new AdminLoginRateLimitDecision
        {
            IsRateLimited = true,
            RetryAfterSeconds = Math.Max(1, (int)Math.Ceiling((state.BlockedUntilUtc.Value - now).TotalSeconds)),
        });
    }

    public Task ResetAsync(AdminLoginAttemptKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _failures.TryRemove(ToCacheKey(key), out _);
        return Task.CompletedTask;
    }

    private static FailureState UpdateState(FailureState existing, DateTimeOffset now)
    {
        if (existing.BlockedUntilUtc is not null && existing.BlockedUntilUtc > now)
        {
            return existing;
        }

        if (existing.WindowStartedUtc + Window <= now)
        {
            return new FailureState
            {
                WindowStartedUtc = now,
                FailedAttempts = 1,
            };
        }

        var failedAttempts = existing.FailedAttempts + 1;
        return new FailureState
        {
            WindowStartedUtc = existing.WindowStartedUtc,
            FailedAttempts = failedAttempts,
            BlockedUntilUtc = failedAttempts >= MaxFailedAttempts
                ? now + Window
                : null,
        };
    }

    private static string ToCacheKey(AdminLoginAttemptKey key)
    {
        return $"{key.NormalizedUsername}|{key.RemoteAddress}";
    }

    private sealed record FailureState
    {
        public required DateTimeOffset WindowStartedUtc { get; init; }

        public int FailedAttempts { get; init; }

        public DateTimeOffset? BlockedUntilUtc { get; init; }
    }
}
