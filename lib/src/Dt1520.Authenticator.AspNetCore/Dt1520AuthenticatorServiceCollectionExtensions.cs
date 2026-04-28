using Dt1520.Authenticator.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.AspNetCore;

/// <summary>
/// ASP.NET Core service registration extensions for DT-1520 Authenticator integrations.
/// </summary>
public static class Dt1520AuthenticatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers DT-1520 Authenticator client and callback validation services from a configuration section.
    /// </summary>
    /// <param name="services">Application service collection.</param>
    /// <param name="configuration">Configuration section containing <see cref="Dt1520AuthenticatorAspNetCoreOptions"/> values.</param>
    /// <returns>The named SDK <see cref="IHttpClientBuilder"/> for optional caller customization.</returns>
    public static IHttpClientBuilder AddDt1520Authenticator(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<Dt1520AuthenticatorAspNetCoreOptions>()
            .Bind(configuration)
            .ValidateOnStart();

        return AddDt1520AuthenticatorCore(services);
    }

    /// <summary>
    /// Registers DT-1520 Authenticator client and callback validation services from code-based options.
    /// </summary>
    /// <param name="services">Application service collection.</param>
    /// <param name="configureOptions">Callback used to configure trusted backend integration settings.</param>
    /// <returns>The named SDK <see cref="IHttpClientBuilder"/> for optional caller customization.</returns>
    public static IHttpClientBuilder AddDt1520Authenticator(
        this IServiceCollection services,
        Action<Dt1520AuthenticatorAspNetCoreOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services
            .AddOptions<Dt1520AuthenticatorAspNetCoreOptions>()
            .Configure(configureOptions)
            .ValidateOnStart();

        return AddDt1520AuthenticatorCore(services);
    }

    private static IHttpClientBuilder AddDt1520AuthenticatorCore(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<Dt1520AuthenticatorAspNetCoreOptions>, Dt1520AuthenticatorAspNetCoreOptionsValidator>());
        services.TryAddSingleton<Dt1520AuthenticatorCallbackValidator>();
        services.TryAddTransient(CreateClient);

        return services.AddHttpClient(Dt1520AuthenticatorAspNetCoreDefaults.HttpClientName);
    }

    private static Dt1520AuthenticatorClient CreateClient(IServiceProvider serviceProvider)
    {
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var options = serviceProvider.GetRequiredService<IOptions<Dt1520AuthenticatorAspNetCoreOptions>>().Value;
        var httpClient = httpClientFactory.CreateClient(Dt1520AuthenticatorAspNetCoreDefaults.HttpClientName);
        return new Dt1520AuthenticatorClient(httpClient, options.ToClientOptions());
    }
}
