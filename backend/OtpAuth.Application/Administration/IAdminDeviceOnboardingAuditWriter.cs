namespace OtpAuth.Application.Administration;

public interface IAdminDeviceOnboardingAuditWriter
{
    Task WriteCreatedAsync(
        AdminContext adminContext,
        AdminDeviceOnboardingView artifact,
        CancellationToken cancellationToken);

    Task WriteRevokedAsync(
        AdminContext adminContext,
        AdminDeviceOnboardingView artifact,
        CancellationToken cancellationToken);
}
