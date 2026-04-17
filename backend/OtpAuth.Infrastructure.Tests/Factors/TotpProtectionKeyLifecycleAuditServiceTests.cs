using OtpAuth.Infrastructure.Factors;
using OtpAuth.Infrastructure.Security;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class TotpProtectionKeyLifecycleAuditServiceTests
{
    [Fact]
    public async Task RecordSnapshotAsync_PersistsSanitizedPayload()
    {
        var store = new InMemoryAuditStore();
        var service = new TotpProtectionKeyLifecycleAuditService(new SecurityAuditService(store));
        var report = new TotpProtectionKeyLifecycleReport
        {
            ObservedAtUtc = DateTimeOffset.UtcNow,
            CurrentKeyVersion = 2,
            ConfiguredKeys =
            [
                new TotpProtectionKeyLifecycleReportKey
                {
                    KeyVersion = 2,
                    IsCurrent = true,
                },
            ],
            UsageByKeyVersion =
            [
                new TotpProtectionKeyLifecycleReportUsage
                {
                    KeyVersion = 2,
                    EnrollmentCount = 1,
                    IsConfigured = true,
                    IsCurrent = true,
                },
            ],
            EnrollmentsRequiringReEncryptionCount = 0,
            Warnings = [],
        };

        var auditEvent = await service.RecordSnapshotAsync(report, CancellationToken.None);

        Assert.Equal("totp_protection_key_lifecycle.snapshot", auditEvent.EventType);
        Assert.Equal("totp_protection_key", auditEvent.SubjectType);
        Assert.Equal("2", auditEvent.SubjectId);
        Assert.Contains("\"currentKeyVersion\":2", auditEvent.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentKey", auditEvent.PayloadJson, StringComparison.Ordinal);
        Assert.Single(store.Events);
    }

    [Fact]
    public async Task ListRecentAsync_UsesTotpPrefixAndNormalizesLimit()
    {
        var store = new InMemoryAuditStore();
        var service = new TotpProtectionKeyLifecycleAuditService(new SecurityAuditService(store));

        _ = await service.ListRecentAsync(0, CancellationToken.None);

        Assert.Equal(10, store.LastRequestedLimit);
        Assert.Equal("totp_protection_key_lifecycle.", store.LastRequestedPrefix);
    }

    private sealed class InMemoryAuditStore : ISecurityAuditStore
    {
        public List<SecurityAuditEvent> Events { get; } = [];
        public int LastRequestedLimit { get; private set; }
        public string? LastRequestedPrefix { get; private set; }

        public Task AppendAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<SecurityAuditEvent>> ListRecentAsync(
            int limit,
            string? eventTypePrefix,
            CancellationToken cancellationToken)
        {
            LastRequestedLimit = limit;
            LastRequestedPrefix = eventTypePrefix;
            return Task.FromResult<IReadOnlyCollection<SecurityAuditEvent>>(Events.Take(limit).ToArray());
        }
    }
}
