using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OtpAuth.Api.Hosting;

public sealed class ReverseProxyForwardingOptions
{
    public bool Enabled { get; init; }

    public int ForwardLimit { get; init; } = 2;

    public string[] KnownProxies { get; init; } = [];

    public string[] KnownNetworks { get; init; } = [];
}

public static class ReverseProxyForwardingExtensions
{
    public static ReverseProxyForwardingOptions AddConfiguredReverseProxyForwarding(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration
            .GetSection("ReverseProxy")
            .Get<ReverseProxyForwardingOptions>() ?? new ReverseProxyForwardingOptions();
        if (!options.Enabled)
        {
            return options;
        }

        builder.Services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
        {
            if (options.ForwardLimit <= 0)
            {
                throw new InvalidOperationException("ReverseProxy:ForwardLimit must be a positive integer.");
            }

            forwardedHeadersOptions.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwardedHeadersOptions.ForwardLimit = options.ForwardLimit;
            forwardedHeadersOptions.RequireHeaderSymmetry = false;
            forwardedHeadersOptions.KnownIPNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();

            foreach (var proxy in options.KnownProxies)
            {
                forwardedHeadersOptions.KnownProxies.Add(ParseIpAddress(proxy, "ReverseProxy:KnownProxies"));
            }

            foreach (var network in options.KnownNetworks)
            {
                forwardedHeadersOptions.KnownIPNetworks.Add(ParseNetwork(network));
            }

            if (forwardedHeadersOptions.KnownIPNetworks.Count == 0 &&
                forwardedHeadersOptions.KnownProxies.Count == 0)
            {
                throw new InvalidOperationException(
                    "ReverseProxy:Enabled requires at least one trusted entry in ReverseProxy:KnownNetworks or ReverseProxy:KnownProxies.");
            }
        });

        return options;
    }

    public static WebApplication UseConfiguredReverseProxyForwarding(
        this WebApplication app,
        ReverseProxyForwardingOptions options)
    {
        if (options.Enabled)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }

    private static IPAddress ParseIpAddress(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !IPAddress.TryParse(value.Trim(), out var address))
        {
            throw new InvalidOperationException($"{settingName} must contain valid IP addresses.");
        }

        return address;
    }

    private static System.Net.IPNetwork ParseNetwork(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("ReverseProxy:KnownNetworks must contain valid CIDR entries.");
        }

        var parts = value.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var prefix) ||
            !int.TryParse(parts[1], out var prefixLength))
        {
            throw new InvalidOperationException("ReverseProxy:KnownNetworks must contain valid CIDR entries.");
        }

        var maxPrefixLength = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefixLength)
        {
            throw new InvalidOperationException("ReverseProxy:KnownNetworks must contain valid CIDR entries.");
        }

        return new System.Net.IPNetwork(prefix, prefixLength);
    }
}
