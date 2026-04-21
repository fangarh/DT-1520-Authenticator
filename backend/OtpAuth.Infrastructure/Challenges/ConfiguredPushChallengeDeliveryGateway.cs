using Microsoft.Extensions.Logging;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class ConfiguredPushChallengeDeliveryGateway : IPushChallengeDeliveryGateway
{
    private readonly ILogger<ConfiguredPushChallengeDeliveryGateway> _logger;
    private readonly IReadOnlyDictionary<string, IPushChallengeDeliveryProviderGateway> _providers;
    private readonly string _providerName;

    public ConfiguredPushChallengeDeliveryGateway(
        IEnumerable<IPushChallengeDeliveryProviderGateway> providers,
        PushChallengeDeliveryGatewayOptions options,
        ILogger<ConfiguredPushChallengeDeliveryGateway> logger)
    {
        _logger = logger;
        _providerName = options.GetProvider();
        _providers = providers.ToDictionary(
            provider => provider.ProviderName,
            StringComparer.Ordinal);
    }

    public Task<PushChallengeDispatchResult> DeliverAsync(
        PushChallengeDispatchRequest request,
        CancellationToken cancellationToken)
    {
        if (_providers.TryGetValue(_providerName, out var provider))
        {
            return provider.DeliverAsync(request, cancellationToken);
        }

        _logger.LogError(
            "Push delivery provider is not registered. Provider={Provider} DeliveryId={DeliveryId} ChallengeId={ChallengeId}",
            _providerName,
            request.DeliveryId,
            request.ChallengeId);

        return Task.FromResult(PushChallengeDispatchResult.Failure("push_provider_not_configured", isRetryable: false));
    }
}
