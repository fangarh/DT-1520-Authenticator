using OtpAuth.Infrastructure.Security;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Security;

public sealed class SecurityAuditServiceTests
{
    [Fact]
    public async Task RecordAsync_TrimsAndPersistsNormalizedEntry()
    {
        var store = new InMemorySecurityAuditStore();
        var service = new SecurityAuditService(store);

        var auditEvent = await service.RecordAsync(
            new SecurityAuditEntry
            {
                EventType = " integration_client_lifecycle.secret_rotated ",
                SubjectType = " integration_client ",
                SubjectId = " crm-client ",
                Summary = " rotated ",
                PayloadJson = "{\"clientId\":\"crm-client\"}",
                Severity = " warning ",
                Source = " migration_runner ",
            },
            CancellationToken.None);

        Assert.Equal("integration_client_lifecycle.secret_rotated", auditEvent.EventType);
        Assert.Equal("integration_client", auditEvent.SubjectType);
        Assert.Equal("crm-client", auditEvent.SubjectId);
        Assert.Equal("rotated", auditEvent.Summary);
        Assert.Equal("warning", auditEvent.Severity);
        Assert.Equal("migration_runner", auditEvent.Source);
        Assert.Single(store.Events);
    }

    [Fact]
    public async Task ListRecentAsync_NormalizesLimitAndPrefix()
    {
        var store = new InMemorySecurityAuditStore();
        var service = new SecurityAuditService(store);

        _ = await service.ListRecentAsync(0, " integration_client_lifecycle. ", CancellationToken.None);

        Assert.Equal(10, store.LastRequestedLimit);
        Assert.Equal("integration_client_lifecycle.", store.LastRequestedPrefix);
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
