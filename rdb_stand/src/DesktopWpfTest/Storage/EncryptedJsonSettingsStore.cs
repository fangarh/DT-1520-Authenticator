using System.IO;
using System.Text.Json;
using Dt1520.Authenticator.DesktopWpfTest.Models;

namespace Dt1520.Authenticator.DesktopWpfTest.Storage;

public sealed class EncryptedJsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly DemoSettingsCrypto _crypto;

    public EncryptedJsonSettingsStore()
        : this(GetDefaultFilePath(), new DemoSettingsCrypto())
    {
    }

    public EncryptedJsonSettingsStore(string filePath, DemoSettingsCrypto? crypto = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = filePath;
        _crypto = crypto ?? new DemoSettingsCrypto();
    }

    public async Task<DemoSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_filePath);
        var envelope = await JsonSerializer.DeserializeAsync<EncryptedSettingsEnvelope>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return envelope is null ? null : _crypto.Decrypt(envelope);
    }

    public async Task SaveAsync(DemoSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var envelope = _crypto.Encrypt(settings);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "DT-1520", "DesktopWpfTest", "settings.demo.json");
    }
}
