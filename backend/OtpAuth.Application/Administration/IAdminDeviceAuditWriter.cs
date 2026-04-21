using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Administration;

public interface IAdminDeviceAuditWriter
{
    Task WriteRevokedAsync(
        AdminContext adminContext,
        RegisteredDevice device,
        CancellationToken cancellationToken);
}
