using OtpAuth.Infrastructure.Integrations;
using OtpAuth.Infrastructure.Security;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class SigningKeyLifecycleAuditServiceTests
{
    [Fact]
    public async Task RecordSnapshotAsync_PersistsSanitizedPayload()
    {
        var report = new BootstrapSigningKeyLifecycleReport
        {
            ObservedAtUtc = DateTimeOffset.UtcNow,
            CurrentSigningKeyId = "key-v2",
            AccessTokenLifetimeMinutes = 60,
            RecommendedLegacyRetirementDelaySeconds = 3630,
            UsesEphemeralCurrentSigningKey = false,
            Keys =
            [
                new BootstrapSigningKeyLifecycleReportKey
                {
                    KeyId = "key-v2",
                    IsCurrent = true,
                    IsAcceptedForValidation = true,
                },
                new BootstrapSigningKeyLifecycleReportKey
                {
                    KeyId = "key-v1",
                    IsCurrent = false,
                    IsAcceptedForValidation = true,
                    RetireAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                },
            ],
            Warnings = ["Legacy keys without RetireAtUtc remain valid indefinitely: key-v0"],
        };
        var store = new InMemorySecurityAuditStore();
        var service = new SigningKeyLifecycleAuditService(new SecurityAuditService(store));

        var auditEvent = await service.RecordSnapshotAsync(report, CancellationToken.None);

        Assert.Equal("signing_key_lifecycle.snapshot", auditEvent.EventType);
        Assert.Equal("signing_key", auditEvent.SubjectType);
        Assert.Equal(report.CurrentSigningKeyId, auditEvent.SubjectId);
        Assert.Equal("warning", auditEvent.Severity);
        Assert.Contains("\"currentSigningKeyId\":\"key-v2\"", auditEvent.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-tests-signing-key", auditEvent.PayloadJson, StringComparison.Ordinal);
        Assert.Single(store.Events);
    }

    [Fact]
    public async Task ListRecentAsync_NormalizesLimit()
    {
        var store = new InMemorySecurityAuditStore();
        var service = new SigningKeyLifecycleAuditService(new SecurityAuditService(store));

        _ = await service.ListRecentAsync(0, CancellationToken.None);

        Assert.Equal(10, store.LastRequestedLimit);
        Assert.Equal("signing_key_lifecycle.", store.LastRequestedPrefix);
    }

    private sealed class InMemorySecurityAuditStore : ISecurityAuditStore
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
