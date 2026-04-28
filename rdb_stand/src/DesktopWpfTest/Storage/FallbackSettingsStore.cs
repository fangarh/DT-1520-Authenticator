using Dt1520.Authenticator.DesktopWpfTest.Models;

namespace Dt1520.Authenticator.DesktopWpfTest.Storage;

public sealed class FallbackSettingsStore : ISettingsStore
{
    private readonly ISettingsStore _primary;
    private readonly ISettingsStore _fallback;

    public FallbackSettingsStore(ISettingsStore primary, ISettingsStore fallback)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    public async Task<DemoSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var primary = await _primary.LoadAsync(cancellationToken).ConfigureAwait(false);
        var fallback = await _fallback.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (primary is null)
        {
            return fallback;
        }

        return fallback is null ? primary : FillMissing(primary, fallback);
    }

    public Task SaveAsync(DemoSettings settings, CancellationToken cancellationToken = default)
    {
        return _primary.SaveAsync(settings, cancellationToken);
    }

    private static DemoSettings FillMissing(DemoSettings primary, DemoSettings fallback)
    {
        return primary with
        {
            ReferenceBackendBaseUrl = Use(primary.ReferenceBackendBaseUrl, fallback.ReferenceBackendBaseUrl),
            ExternalUserId = Use(primary.ExternalUserId, fallback.ExternalUserId),
            OperationDisplayName = Use(primary.OperationDisplayName, fallback.OperationDisplayName),
            Dt1520BaseUrl = Use(primary.Dt1520BaseUrl, fallback.Dt1520BaseUrl),
            TenantId = Use(primary.TenantId, fallback.TenantId),
            ApplicationClientId = Use(primary.ApplicationClientId, fallback.ApplicationClientId),
            ClientId = Use(primary.ClientId, fallback.ClientId),
            ClientSecret = Use(primary.ClientSecret, fallback.ClientSecret),
            CallbackSigningSecret = Use(primary.CallbackSigningSecret, fallback.CallbackSigningSecret),
            CallbackUrl = Use(primary.CallbackUrl, fallback.CallbackUrl),
            Scope = Use(primary.Scope, fallback.Scope),
        };
    }

    private static string Use(string primary, string fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback : primary;
    }
}
