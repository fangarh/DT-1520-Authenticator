using System.IO;
using System.Text.Json;
using Dt1520.Authenticator.DesktopWpfTest.Models;

namespace Dt1520.Authenticator.DesktopWpfTest.Storage;

public sealed class ReferenceBackendDevelopmentSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _filePath;

    public ReferenceBackendDevelopmentSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = filePath;
    }

    public static ReferenceBackendDevelopmentSettingsStore FromCurrentAppDirectory()
    {
        return new ReferenceBackendDevelopmentSettingsStore(FindReferenceBackendSettingsPath());
    }

    public async Task<DemoSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_filePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        var authenticator = root.TryGetProperty("Dt1520Authenticator", out var authSection)
            ? authSection
            : default;
        var referenceBackend = root.TryGetProperty("ReferenceBackend", out var backendSection)
            ? backendSection
            : default;

        return new DemoSettings
        {
            ReferenceBackendBaseUrl = Environment.GetEnvironmentVariable("RDB_BACKEND_BASE_URL")
                ?? "http://127.0.0.1:5188/",
            Dt1520BaseUrl = ReadString(authenticator, "BaseUrl") ?? "https://admin.ghostring.ru:18443/",
            ClientId = ReadString(authenticator, "ClientId") ?? string.Empty,
            ClientSecret = ReadString(authenticator, "ClientSecret") ?? string.Empty,
            CallbackSigningSecret = ReadString(authenticator, "CallbackSigningSecret") ?? string.Empty,
            Scope = ReadString(authenticator, "Scope") ?? "challenges:read challenges:write devices:read",
            TenantId = ReadString(referenceBackend, "TenantId") ?? string.Empty,
            ApplicationClientId = ReadString(referenceBackend, "ApplicationClientId") ?? string.Empty,
            CallbackUrl = ReadString(referenceBackend, "CallbackUrl") ?? string.Empty,
        };
    }

    public Task SaveAsync(DemoSettings settings, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Reference backend development settings are read-only for the WPF demo.");
    }

    private static string FindReferenceBackendSettingsPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "ReferenceBackend",
                "appsettings.Development.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "src",
            "ReferenceBackend",
            "appsettings.Development.json");
    }

    private static string? ReadString(JsonElement section, string propertyName)
    {
        if (section.ValueKind != JsonValueKind.Object
            || !section.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}
