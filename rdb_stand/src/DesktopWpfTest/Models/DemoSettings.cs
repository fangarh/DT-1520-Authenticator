namespace Dt1520.Authenticator.DesktopWpfTest.Models;

public sealed record DemoSettings
{
    public string ReferenceBackendBaseUrl { get; init; } = "http://127.0.0.1:5188/";

    public string ExternalUserId { get; init; } = string.Empty;

    public string OperationDisplayName { get; init; } = "Reference WPF protected operation";

    public string Dt1520BaseUrl { get; init; } = "https://admin.ghostring.ru:18443/";

    public string TenantId { get; init; } = string.Empty;

    public string ApplicationClientId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string CallbackSigningSecret { get; init; } = string.Empty;

    public string CallbackUrl { get; init; } = string.Empty;

    public string Scope { get; init; } = "challenges:read challenges:write devices:read";
}
