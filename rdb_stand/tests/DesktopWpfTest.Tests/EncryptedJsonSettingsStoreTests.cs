using Dt1520.Authenticator.DesktopWpfTest.Models;
using Dt1520.Authenticator.DesktopWpfTest.Storage;

namespace Dt1520.Authenticator.DesktopWpfTest.Tests;

public sealed class EncryptedJsonSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsSettings()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var store = new EncryptedJsonSettingsStore(filePath, new DemoSettingsCrypto("test-profile"));
        var settings = CreateSettings();

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal(settings, loaded);
    }

    [Fact]
    public async Task SaveAsync_DoesNotWritePlaintextSecrets()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var store = new EncryptedJsonSettingsStore(filePath, new DemoSettingsCrypto("test-profile"));
        var settings = CreateSettings();

        await store.SaveAsync(settings);
        var storedJson = await File.ReadAllTextAsync(filePath);

        Assert.DoesNotContain(settings.ClientSecret, storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(settings.CallbackSigningSecret, storedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(settings.ExternalUserId, storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsMissing_ReturnsNull()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var store = new EncryptedJsonSettingsStore(filePath, new DemoSettingsCrypto("test-profile"));

        var loaded = await store.LoadAsync();

        Assert.Null(loaded);
    }

    [Fact]
    public async Task FallbackStore_LoadsBackendDevelopmentSettingsWhenEncryptedFileIsMissing()
    {
        var encryptedPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var backendSettingsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await File.WriteAllTextAsync(backendSettingsPath, """
            {
              "Dt1520Authenticator": {
                "BaseUrl": "https://admin.example.test/",
                "ClientId": "mvp-test-console",
                "ClientSecret": "demo-secret",
                "CallbackSigningSecret": "",
                "Scope": "challenges:read challenges:write devices:read"
              },
              "ReferenceBackend": {
                "TenantId": "4204F38F-D5FC-4161-A0D2-81588C96785D",
                "ApplicationClientId": "2B9E81BC-4D32-436B-8E21-83557D765C43",
                "CallbackUrl": ""
              }
            }
            """);
        var store = new FallbackSettingsStore(
            new EncryptedJsonSettingsStore(encryptedPath, new DemoSettingsCrypto("test-profile")),
            new ReferenceBackendDevelopmentSettingsStore(backendSettingsPath));

        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("https://admin.example.test/", loaded.Dt1520BaseUrl);
        Assert.Equal("mvp-test-console", loaded.ClientId);
        Assert.Equal("demo-secret", loaded.ClientSecret);
        Assert.Equal("4204F38F-D5FC-4161-A0D2-81588C96785D", loaded.TenantId);
    }

    [Fact]
    public async Task FallbackStore_SaveAsync_WritesEncryptedPrimaryStore()
    {
        var encryptedPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var fallbackPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var store = new FallbackSettingsStore(
            new EncryptedJsonSettingsStore(encryptedPath, new DemoSettingsCrypto("test-profile")),
            new ReferenceBackendDevelopmentSettingsStore(fallbackPath));

        await store.SaveAsync(CreateSettings());
        var storedJson = await File.ReadAllTextAsync(encryptedPath);

        Assert.DoesNotContain("plain-secret-value-for-test", storedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FallbackStore_LoadAsync_FillsEmptyPrimaryFieldsFromBackendSettings()
    {
        var encryptedPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var backendSettingsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var primary = new EncryptedJsonSettingsStore(encryptedPath, new DemoSettingsCrypto("test-profile"));
        await primary.SaveAsync(CreateSettings() with
        {
            CallbackSigningSecret = "",
            CallbackUrl = "",
        });
        await File.WriteAllTextAsync(backendSettingsPath, """
            {
              "Dt1520Authenticator": {
                "CallbackSigningSecret": "callback-secret-from-backend"
              },
              "ReferenceBackend": {
                "CallbackUrl": "https://reference.example.test/api/reference/callbacks/dt1520"
              }
            }
            """);
        var store = new FallbackSettingsStore(
            primary,
            new ReferenceBackendDevelopmentSettingsStore(backendSettingsPath));

        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("callback-secret-from-backend", loaded.CallbackSigningSecret);
        Assert.Equal("https://reference.example.test/api/reference/callbacks/dt1520", loaded.CallbackUrl);
        Assert.Equal("demo-user-42", loaded.ExternalUserId);
    }

    private static DemoSettings CreateSettings()
    {
        return new DemoSettings
        {
            ReferenceBackendBaseUrl = "http://127.0.0.1:5188/",
            ExternalUserId = "demo-user-42",
            OperationDisplayName = "Demo operation",
            Dt1520BaseUrl = "https://admin.example.test/",
            TenantId = "4204F38F-D5FC-4161-A0D2-81588C96785D",
            ApplicationClientId = "2B9E81BC-4D32-436B-8E21-83557D765C43",
            ClientId = "mvp-test-console",
            ClientSecret = "plain-secret-value-for-test",
            CallbackSigningSecret = "callback-secret-value-for-test",
            CallbackUrl = "https://callback.example.test/api/reference/callbacks/dt1520",
            Scope = "challenges:read challenges:write devices:read",
        };
    }
}
