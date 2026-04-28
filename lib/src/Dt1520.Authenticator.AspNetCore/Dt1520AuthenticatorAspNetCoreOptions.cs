using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.AspNetCore;

/// <summary>
/// ASP.NET Core configuration for trusted backend integration with DT-1520 Authenticator.
/// </summary>
public sealed class Dt1520AuthenticatorAspNetCoreOptions
{
    /// <summary>
    /// Base URL of the DT-1520 Authenticator runtime.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Integration client identifier issued by DT-1520.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Integration client secret. Store this value only in trusted server-side configuration.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Shared callback signing secret used to validate DT-1520 callbacks and webhooks.
    /// </summary>
    public string? CallbackSigningSecret { get; set; }

    /// <summary>
    /// Optional OAuth scope string requested from DT-1520.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Per-request timeout used by SDK HTTP operations. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Time subtracted from token expiry before reusing a cached access token. Defaults to one minute.
    /// </summary>
    public TimeSpan TokenExpirySkew { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Allowed difference between the supplied callback timestamp and current time.
    /// </summary>
    public TimeSpan CallbackTimestampTolerance { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum callback request body size read by callback validation helpers. Defaults to 128 KiB.
    /// </summary>
    public long MaxCallbackBodyBytes { get; set; } = 128 * 1024;

    /// <summary>
    /// Optional product token used in the SDK <c>User-Agent</c> header.
    /// </summary>
    public string? ProductName { get; set; }

    /// <summary>
    /// Optional product version used in the SDK <c>User-Agent</c> header.
    /// </summary>
    public string? ProductVersion { get; set; }

    internal Dt1520AuthenticatorClientOptions ToClientOptions()
    {
        return new Dt1520AuthenticatorClientOptions
        {
            BaseUrl = BaseUrl ?? new Uri("https://invalid.local/"),
            Credentials = new Dt1520AuthenticatorClientCredentials(ClientId ?? string.Empty, ClientSecret ?? string.Empty),
            Scope = Scope,
            RequestTimeout = RequestTimeout,
            TokenExpirySkew = TokenExpirySkew,
            ProductName = ProductName,
            ProductVersion = ProductVersion,
        };
    }

    internal CallbackSignatureVerifierOptions ToCallbackVerifierOptions()
    {
        return new CallbackSignatureVerifierOptions
        {
            SigningSecret = CallbackSigningSecret ?? string.Empty,
            TimestampTolerance = CallbackTimestampTolerance,
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(Dt1520AuthenticatorAspNetCoreOptions)} {{ {nameof(BaseUrl)} = {BaseUrl}, {nameof(ClientId)} = {ClientId}, {nameof(ClientSecret)} = [redacted], {nameof(CallbackSigningSecret)} = [redacted] }}";
    }
}
