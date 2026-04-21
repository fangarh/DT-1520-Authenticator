using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public static class PushChallengeDeliveryServiceCollectionExtensions
{
    public static IServiceCollection AddPushChallengeDeliveryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("PushDelivery")
            .Get<PushChallengeDeliveryGatewayOptions>() ?? new PushChallengeDeliveryGatewayOptions();
        options.Validate();

        services.AddSingleton(options);
        services.TryAddSingleton(new HttpClient());
        services.AddSingleton<IPushChallengeDeliveryStore, PostgresPushChallengeDeliveryStore>();
        services.AddSingleton<IFcmAccessTokenProvider, GoogleCredentialFcmAccessTokenProvider>();
        services.AddSingleton<IPushChallengeDeliveryProviderGateway, LoggingPushChallengeDeliveryGateway>();
        services.AddSingleton<IPushChallengeDeliveryProviderGateway, FcmPushChallengeDeliveryGateway>();
        services.AddSingleton<IPushChallengeDeliveryGateway, ConfiguredPushChallengeDeliveryGateway>();
        services.AddSingleton<PushChallengeDeliveryCoordinator>();

        return services;
    }
}
