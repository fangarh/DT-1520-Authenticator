using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Infrastructure.Tests.Devices;

internal sealed class RecordingDeviceLifecycleAuditWriter : IDeviceLifecycleAuditWriter
{
    public List<string> Events { get; } = [];

    public Task WriteActivatedAsync(RegisteredDevice device, CancellationToken cancellationToken)
    {
        Events.Add($"activated:{device.Id}");
        return Task.CompletedTask;
    }

    public Task WriteTokenRefreshedAsync(RegisteredDevice device, CancellationToken cancellationToken)
    {
        Events.Add($"token_refreshed:{device.Id}");
        return Task.CompletedTask;
    }

    public Task WriteRefreshReuseDetectedAsync(RegisteredDevice device, string tokenState, CancellationToken cancellationToken)
    {
        Events.Add($"refresh_reuse_detected:{device.Id}:{tokenState}");
        return Task.CompletedTask;
    }

    public Task WriteRevokedAsync(RegisteredDevice device, bool stateChanged, CancellationToken cancellationToken)
    {
        Events.Add($"revoked:{device.Id}:{stateChanged}");
        return Task.CompletedTask;
    }

    public Task WriteBlockedAsync(RegisteredDevice device, string reason, bool stateChanged, CancellationToken cancellationToken)
    {
        Events.Add($"blocked:{device.Id}:{reason}:{stateChanged}");
        return Task.CompletedTask;
    }
}
