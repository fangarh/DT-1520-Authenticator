using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.ReferenceBackend;

public static class DependencyInjection
{
    public static IServiceCollection AddReferenceBackend(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ReferenceBackendOptions>()
            .Bind(configuration.GetSection("ReferenceBackend"))
            .Validate(options => options.Validate().Count == 0, "ReferenceBackend configuration is invalid.")
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ReferenceBackendOptions>, ReferenceBackendOptionsValidator>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProtectedOperationStore, InMemoryProtectedOperationStore>();
        services.AddSingleton<ReferenceBackendReadinessReporter>();
        services.AddTransient<IReferenceAuthenticatorGateway, SdkReferenceAuthenticatorGateway>();
        services.AddTransient<ProtectedOperationCoordinator>();
        return services;
    }
}
