using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Administration;
using OtpAuth.Infrastructure.Security;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminIntegrationClientAuditWriterTests
{
    [Theory]
    [InlineData("created")]
    [InlineData("secret_rotated")]
    [InlineData("scopes_changed")]
    [InlineData("deactivated")]
    [InlineData("reactivated")]
    public async Task WriteLifecycleEventAsync_RecordsSanitizedPayloadWithoutSecretMaterial(string action)
    {
        var store = new InMemorySecurityAuditStore();
        var writer = new AdminIntegrationClientAuditWriter(new SecurityAuditService(store));
        var adminContext = new AdminContext
        {
            AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Username = "operator",
            Permissions = [AdminPermissions.IntegrationClientsWrite],
        };
        var client = new AdminIntegrationClientView
        {
            ClientId = "otpauth-crm",
            TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ApplicationClientId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Status = AdminIntegrationClientStatus.Active,
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow,
            LastSecretRotatedUtc = DateTimeOffset.UtcNow,
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow,
        };

        await WriteAsync(writer, action, adminContext, client);

        var auditEvent = Assert.Single(store.Events);
        Assert.Equal($"admin_integration_client.{action}", auditEvent.EventType);
        Assert.Equal("integration_client", auditEvent.SubjectType);
        Assert.Equal("otpauth-crm", auditEvent.SubjectId);
        Assert.DoesNotContain("clientSecret", auditEvent.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client_secret", auditEvent.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-hash", auditEvent.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pbkdf2", auditEvent.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    private static Task WriteAsync(
        AdminIntegrationClientAuditWriter writer,
        string action,
        AdminContext adminContext,
        AdminIntegrationClientView client)
    {
        return action switch
        {
            "created" => writer.WriteCreatedAsync(adminContext, client, CancellationToken.None),
            "secret_rotated" => writer.WriteSecretRotatedAsync(adminContext, client, CancellationToken.None),
            "scopes_changed" => writer.WriteScopesChangedAsync(adminContext, client, CancellationToken.None),
            "deactivated" => writer.WriteDeactivatedAsync(adminContext, client, CancellationToken.None),
            "reactivated" => writer.WriteReactivatedAsync(adminContext, client, CancellationToken.None),
            _ => throw new InvalidOperationException($"Unsupported action '{action}'."),
        };
    }

    private sealed class InMemorySecurityAuditStore : ISecurityAuditStore
    {
        public List<SecurityAuditEvent> Events { get; } = [];

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
            return Task.FromResult<IReadOnlyCollection<SecurityAuditEvent>>(Events);
        }
    }
}
