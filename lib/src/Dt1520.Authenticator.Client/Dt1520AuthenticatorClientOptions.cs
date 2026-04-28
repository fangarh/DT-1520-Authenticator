using System.Net.Http.Headers;

namespace Dt1520.Authenticator.Client;

/// <summary>
/// Runtime options for <see cref="Dt1520AuthenticatorClient"/>.
/// </summary>
public sealed class Dt1520AuthenticatorClientOptions
{
    /// <summary>
    /// Base URL of the DT-1520 Authenticator runtime, for example <c>https://auth.example.test/</c>.
    /// </summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>
    /// Integration client credentials used for OAuth 2.0 <c>client_credentials</c> token acquisition.
    /// </summary>
    public required Dt1520AuthenticatorClientCredentials Credentials { get; init; }

    /// <summary>
    /// Optional OAuth scope string requested from DT-1520. When omitted, server-side default client scopes are used.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Per-request timeout used by SDK HTTP operations. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Time subtracted from token expiry before reusing a cached access token. Defaults to one minute.
    /// </summary>
    public TimeSpan TokenExpirySkew { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Optional product token used in the SDK <c>User-Agent</c> header.
    /// </summary>
    public string? ProductName { get; init; }

    /// <summary>
    /// Optional product version used in the SDK <c>User-Agent</c> header.
    /// </summary>
    public string? ProductVersion { get; init; }

    internal Dt1520AuthenticatorValidatedOptions Validate()
    {
        if (!BaseUrl.IsAbsoluteUri || !IsHttpScheme(BaseUrl))
        {
            throw new ArgumentException("BaseUrl must be an absolute http or https URL.", nameof(BaseUrl));
        }

        if (!string.IsNullOrEmpty(BaseUrl.Query) || !string.IsNullOrEmpty(BaseUrl.Fragment))
        {
            throw new ArgumentException("BaseUrl must not include query string or fragment components.", nameof(BaseUrl));
        }

        if (string.IsNullOrWhiteSpace(Credentials.ClientId))
        {
            throw new ArgumentException("ClientId is required.", nameof(Credentials));
        }

        if (string.IsNullOrWhiteSpace(Credentials.ClientSecret))
        {
            throw new ArgumentException("ClientSecret is required.", nameof(Credentials));
        }

        if (RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout), "RequestTimeout must be greater than zero.");
        }

        if (TokenExpirySkew < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(TokenExpirySkew), "TokenExpirySkew must not be negative.");
        }

        ValidateUserAgentProduct(ProductName, ProductVersion);

        return new Dt1520AuthenticatorValidatedOptions(
            EnsureTrailingSlash(BaseUrl),
            Credentials,
            Scope,
            RequestTimeout,
            TokenExpirySkew,
            ProductName,
            ProductVersion);
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var value = uri.AbsoluteUri;
        return value.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(value + "/");
    }

    private static void ValidateUserAgentProduct(string? productName, string? productVersion)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return;
        }

        try
        {
            _ = string.IsNullOrWhiteSpace(productVersion)
                ? new ProductInfoHeaderValue(productName)
                : new ProductInfoHeaderValue(productName, productVersion);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "ProductName and ProductVersion must be valid HTTP User-Agent product tokens.",
                nameof(ProductName),
                exception);
        }
    }
}

internal sealed record Dt1520AuthenticatorValidatedOptions(
    Uri BaseUrl,
    Dt1520AuthenticatorClientCredentials Credentials,
    string? Scope,
    TimeSpan RequestTimeout,
    TimeSpan TokenExpirySkew,
    string? ProductName,
    string? ProductVersion);
