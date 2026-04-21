using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Tests.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminRevokeUserDeviceHandlerTests
{
    [Fact]
    public async Task HandleAsync_RevokesMatchingActiveDevice_AndWritesAudit()
    {
        var store = new InMemoryDeviceRegistryStore();
        var seeded = store.SeedActiveDevice(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "user-123",
            "installation-active");
        var auditWriter = new RecordingDeviceLifecycleAuditWriter();
        var adminAuditWriter = new AdminAuthApiTestFactory.RecordingAdminDeviceAuditWriter();
        var handler = new AdminRevokeUserDeviceHandler(store, auditWriter, adminAuditWriter);
        var adminUserId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            new AdminRevokeUserDeviceRequest
            {
                TenantId = seeded.Device.TenantId,
                ExternalUserId = "  user-123  ",
                DeviceId = seeded.Device.Id,
            },
            new AdminContext
            {
                AdminUserId = adminUserId,
                Username = "operator",
                Permissions = [AdminPermissions.DevicesWrite],
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Device);
        Assert.Equal(AdminDeviceLifecycleStatus.Revoked, result.Device!.Status);
        Assert.Contains(auditWriter.Events, entry => entry == $"revoked:{seeded.Device.Id}:True");
        Assert.Contains(
            adminAuditWriter.Events,
            entry => entry.AdminUserId == adminUserId &&
                     entry.DeviceId == seeded.Device.Id &&
                     entry.Status == "revoked" &&
                     entry.IsPushCapable);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenPermissionIsMissing()
    {
        var store = new InMemoryDeviceRegistryStore();
        var seeded = store.SeedActiveDevice(Guid.NewGuid(), Guid.NewGuid(), "user-123", "installation-access");
        var handler = new AdminRevokeUserDeviceHandler(
            store,
            new RecordingDeviceLifecycleAuditWriter(),
            new AdminAuthApiTestFactory.RecordingAdminDeviceAuditWriter());

        var result = await handler.HandleAsync(
            new AdminRevokeUserDeviceRequest
            {
                TenantId = seeded.Device.TenantId,
                ExternalUserId = "user-123",
                DeviceId = seeded.Device.Id,
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminRevokeUserDeviceErrorCode.AccessDenied, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenDeviceDoesNotBelongToRequestedUser()
    {
        var store = new InMemoryDeviceRegistryStore();
        var seeded = store.SeedActiveDevice(Guid.NewGuid(), Guid.NewGuid(), "user-123", "installation-not-found");
        var handler = new AdminRevokeUserDeviceHandler(
            store,
            new RecordingDeviceLifecycleAuditWriter(),
            new AdminAuthApiTestFactory.RecordingAdminDeviceAuditWriter());

        var result = await handler.HandleAsync(
            new AdminRevokeUserDeviceRequest
            {
                TenantId = seeded.Device.TenantId,
                ExternalUserId = "user-999",
                DeviceId = seeded.Device.Id,
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.DevicesWrite],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminRevokeUserDeviceErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsConflict_WhenDeviceIsAlreadyBlocked()
    {
        var store = new InMemoryDeviceRegistryStore();
        var seeded = store.SeedActiveDevice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-123",
            "installation-blocked",
            status: DeviceStatus.Blocked);
        var handler = new AdminRevokeUserDeviceHandler(
            store,
            new RecordingDeviceLifecycleAuditWriter(),
            new AdminAuthApiTestFactory.RecordingAdminDeviceAuditWriter());

        var result = await handler.HandleAsync(
            new AdminRevokeUserDeviceRequest
            {
                TenantId = seeded.Device.TenantId,
                ExternalUserId = "user-123",
                DeviceId = seeded.Device.Id,
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.DevicesWrite],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminRevokeUserDeviceErrorCode.Conflict, result.ErrorCode);
    }
}
