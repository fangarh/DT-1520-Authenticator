using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminListUserDevicesHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsDevices_WhenRequestIsValid()
    {
        var request = new AdminUserDeviceListRequest
        {
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ExternalUserId = "  user-123  ",
        };
        var expectedDevice = new AdminUserDeviceView
        {
            DeviceId = Guid.NewGuid(),
            Platform = DevicePlatform.Android,
            Status = AdminDeviceLifecycleStatus.Active,
            IsPushCapable = true,
            ActivatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
            LastSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        var store = new StubAdminDeviceStore([expectedDevice]);
        var handler = new AdminListUserDevicesHandler(store);

        var result = await handler.HandleAsync(
            request,
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.DevicesRead],
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(request.TenantId, store.LastRequest!.TenantId);
        Assert.Equal("user-123", store.LastRequest.ExternalUserId);
        Assert.Equal(expectedDevice, Assert.Single(result.Devices));
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailed_WhenExternalUserIdIsMissing()
    {
        var handler = new AdminListUserDevicesHandler(new StubAdminDeviceStore([]));

        var result = await handler.HandleAsync(
            new AdminUserDeviceListRequest
            {
                TenantId = Guid.NewGuid(),
                ExternalUserId = "   ",
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.DevicesRead],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListUserDevicesErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenPermissionIsMissing()
    {
        var handler = new AdminListUserDevicesHandler(new StubAdminDeviceStore([]));

        var result = await handler.HandleAsync(
            new AdminUserDeviceListRequest
            {
                TenantId = Guid.NewGuid(),
                ExternalUserId = "user-123",
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListUserDevicesErrorCode.AccessDenied, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenStoreHasNoDevices()
    {
        var handler = new AdminListUserDevicesHandler(new StubAdminDeviceStore([]));

        var result = await handler.HandleAsync(
            new AdminUserDeviceListRequest
            {
                TenantId = Guid.NewGuid(),
                ExternalUserId = "user-404",
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.DevicesRead],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListUserDevicesErrorCode.NotFound, result.ErrorCode);
    }

    private sealed class StubAdminDeviceStore : IAdminDeviceStore
    {
        private readonly IReadOnlyCollection<AdminUserDeviceView> _devices;

        public StubAdminDeviceStore(IReadOnlyCollection<AdminUserDeviceView> devices)
        {
            _devices = devices;
        }

        public AdminUserDeviceListRequest? LastRequest { get; private set; }

        public Task<IReadOnlyCollection<AdminUserDeviceView>> ListByExternalUserAsync(
            AdminUserDeviceListRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_devices);
        }
    }
}
