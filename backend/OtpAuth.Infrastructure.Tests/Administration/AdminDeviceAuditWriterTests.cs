using System.Text.Json;
using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Administration;
using OtpAuth.Infrastructure.Security;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminDeviceAuditWriterTests
{
    [Fact]
    public async Task WriteRevokedAsync_WritesSanitizedAdminAuditPayload()
    {
        var store = new InMemorySecurityAuditStore();
        var writer = new AdminDeviceAuditWriter(new SecurityAuditService(store));
        var device = RegisteredDevice.Activate(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "user-123",
            DevicePlatform.Android,
            "installation-secret",
            "Pixel 10 Pro",
            "push-token",
            "public-key",
            DateTimeOffset.Parse("2026-04-20T10:00:00Z")).MarkRevoked(DateTimeOffset.Parse("2026-04-20T10:05:00Z"));

        await writer.WriteRevokedAsync(
            new AdminContext
            {
                AdminUserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Username = "operator",
                Permissions = [AdminPermissions.DevicesWrite],
            },
            device,
            CancellationToken.None);

        var auditEvent = Assert.Single(store.Events);
        Assert.Equal("admin_device.revoked", auditEvent.EventType);
        Assert.Equal("device", auditEvent.SubjectType);
        Assert.Equal(device.Id.ToString("D"), auditEvent.SubjectId);
        Assert.Equal("warning", auditEvent.Severity);
        Assert.Equal("admin_api", auditEvent.Source);

        using var payload = JsonDocument.Parse(auditEvent.PayloadJson);
        var root = payload.RootElement;
        Assert.Equal("44444444-4444-4444-4444-444444444444", root.GetProperty("adminUserId").GetString());
        Assert.Equal("operator", root.GetProperty("adminUsername").GetString());

        var action = root.GetProperty("action");
        Assert.Equal(device.Id.ToString("D"), action.GetProperty("deviceId").GetString());
        Assert.Equal("user-123", action.GetProperty("externalUserId").GetString());
        Assert.Equal("android", action.GetProperty("platform").GetString());
        Assert.Equal("revoked", action.GetProperty("status").GetString());
        Assert.True(action.GetProperty("isPushCapable").GetBoolean());
        Assert.False(action.TryGetProperty("installationId", out _));
        Assert.False(action.TryGetProperty("deviceName", out _));
        Assert.False(action.TryGetProperty("pushToken", out _));
        Assert.False(action.TryGetProperty("publicKey", out _));
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
            return Task.FromResult<IReadOnlyCollection<SecurityAuditEvent>>(Events.Take(limit).ToArray());
        }
    }
}
