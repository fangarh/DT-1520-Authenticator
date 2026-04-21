using System.Net;

namespace OtpAuth.Application.Webhooks;

public static class WebhookSubscriptionEndpointValidator
{
    public static string? Validate(Uri endpointUrl)
    {
        ArgumentNullException.ThrowIfNull(endpointUrl);

        if (!endpointUrl.IsAbsoluteUri || endpointUrl.Scheme != Uri.UriSchemeHttps)
        {
            return "Webhook endpoint must use HTTPS.";
        }

        if (string.Equals(endpointUrl.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return "Webhook endpoint must not target localhost or private network IP literals.";
        }

        if (!IPAddress.TryParse(endpointUrl.Host, out var ipAddress))
        {
            return null;
        }

        if (IPAddress.IsLoopback(ipAddress))
        {
            return "Webhook endpoint must not target localhost or private network IP literals.";
        }

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return ipAddress.IsIPv6LinkLocal ||
                   ipAddress.IsIPv6SiteLocal ||
                   ipAddress.GetAddressBytes() is [>= 0xfc and <= 0xfd, ..]
                ? "Webhook endpoint must not target localhost or private network IP literals."
                : null;
        }

        var bytes = ipAddress.GetAddressBytes();
        return bytes is [10, ..] ||
               bytes is [127, ..] ||
               bytes is [172, >= 16 and <= 31, ..] ||
               bytes is [192, 168, ..] ||
               bytes is [169, 254, ..]
            ? "Webhook endpoint must not target localhost or private network IP literals."
            : null;
    }
}
