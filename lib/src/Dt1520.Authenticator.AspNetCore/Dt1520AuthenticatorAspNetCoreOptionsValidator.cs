using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.AspNetCore;

internal sealed class Dt1520AuthenticatorAspNetCoreOptionsValidator : IValidateOptions<Dt1520AuthenticatorAspNetCoreOptions>
{
    public ValidateOptionsResult Validate(string? name, Dt1520AuthenticatorAspNetCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        ValidateBaseUrl(options, failures);
        ValidateRequired(options.ClientId, nameof(options.ClientId), failures);
        ValidateRequired(options.ClientSecret, nameof(options.ClientSecret), failures);
        ValidateRequired(options.CallbackSigningSecret, nameof(options.CallbackSigningSecret), failures);
        ValidatePositive(options.RequestTimeout, nameof(options.RequestTimeout), failures);
        ValidateNonNegative(options.TokenExpirySkew, nameof(options.TokenExpirySkew), failures);
        ValidateNonNegative(options.CallbackTimestampTolerance, nameof(options.CallbackTimestampTolerance), failures);

        if (options.MaxCallbackBodyBytes <= 0)
        {
            failures.Add($"{nameof(options.MaxCallbackBodyBytes)} must be greater than zero.");
        }

        ValidateUserAgent(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateBaseUrl(Dt1520AuthenticatorAspNetCoreOptions options, List<string> failures)
    {
        if (options.BaseUrl is null)
        {
            failures.Add($"{nameof(options.BaseUrl)} is required.");
            return;
        }

        var hasHttpScheme = string.Equals(options.BaseUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(options.BaseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!options.BaseUrl.IsAbsoluteUri || !hasHttpScheme)
        {
            failures.Add($"{nameof(options.BaseUrl)} must be an absolute http or https URL.");
        }

        if (!string.IsNullOrEmpty(options.BaseUrl.Query) || !string.IsNullOrEmpty(options.BaseUrl.Fragment))
        {
            failures.Add($"{nameof(options.BaseUrl)} must not include query string or fragment components.");
        }
    }

    private static void ValidateRequired(string? value, string optionName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{optionName} is required.");
        }
    }

    private static void ValidatePositive(TimeSpan value, string optionName, List<string> failures)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"{optionName} must be greater than zero.");
        }
    }

    private static void ValidateNonNegative(TimeSpan value, string optionName, List<string> failures)
    {
        if (value < TimeSpan.Zero)
        {
            failures.Add($"{optionName} must not be negative.");
        }
    }

    private static void ValidateUserAgent(Dt1520AuthenticatorAspNetCoreOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.ProductName))
        {
            return;
        }

        try
        {
            _ = string.IsNullOrWhiteSpace(options.ProductVersion)
                ? new ProductInfoHeaderValue(options.ProductName)
                : new ProductInfoHeaderValue(options.ProductName, options.ProductVersion);
        }
        catch (FormatException)
        {
            failures.Add($"{nameof(options.ProductName)} and {nameof(options.ProductVersion)} must be valid HTTP User-Agent product tokens.");
        }
    }
}
