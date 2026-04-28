using Dt1520.Authenticator.DesktopWpfTest.Models;

namespace Dt1520.Authenticator.DesktopWpfTest.Storage;

public interface ISettingsStore
{
    Task<DemoSettings?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DemoSettings settings, CancellationToken cancellationToken = default);
}
