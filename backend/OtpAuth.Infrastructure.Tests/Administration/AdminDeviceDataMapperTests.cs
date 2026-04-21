using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminDeviceDataMapperTests
{
    [Fact]
    public void ToDomainModel_MapsSafeLifecycleMetadata_ForActivePushCapableDevice()
    {
        var activatedAtUtc = DateTimeOffset.Parse("2026-04-20T12:00:00Z");
        var lastSeenUtc = DateTimeOffset.Parse("2026-04-20T12:05:00Z");
        var model = new AdminDevicePersistenceModel
        {
            DeviceId = Guid.NewGuid(),
            Platform = DevicePlatform.Android,
            Status = DeviceStatus.Active,
            IsPushCapable = true,
            ActivatedUtc = activatedAtUtc,
            LastSeenUtc = lastSeenUtc,
        };

        var view = AdminDeviceDataMapper.ToDomainModel(model);

        Assert.Equal(model.DeviceId, view.DeviceId);
        Assert.Equal(DevicePlatform.Android, view.Platform);
        Assert.Equal(AdminDeviceLifecycleStatus.Active, view.Status);
        Assert.True(view.IsPushCapable);
        Assert.Equal(activatedAtUtc, view.ActivatedUtc);
        Assert.Equal(lastSeenUtc, view.LastSeenUtc);
    }

    [Fact]
    public void ToDomainModel_Throws_WhenStatusIsNotOperatorVisible()
    {
        var model = new AdminDevicePersistenceModel
        {
            DeviceId = Guid.NewGuid(),
            Platform = DevicePlatform.Android,
            Status = DeviceStatus.Pending,
            IsPushCapable = false,
        };

        var exception = Assert.Throws<InvalidOperationException>(() => AdminDeviceDataMapper.ToDomainModel(model));

        Assert.Equal("Unsupported admin device status 'Pending'.", exception.Message);
    }
}
