using Microsoft.Extensions.Logging;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class LoggingPushChallengeDeliveryGateway : IPushChallengeDeliveryProviderGateway
{
    private readonly ILogger<LoggingPushChallengeDeliveryGateway> _logger;

    public LoggingPushChallengeDeliveryGateway(ILogger<LoggingPushChallengeDeliveryGateway> logger)
    {
        _logger = logger;
    }

    public string ProviderName => PushChallengeDeliveryProviderNames.Logging;

    public Task<PushChallengeDispatchResult> DeliverAsync(
        PushChallengeDispatchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.PushToken))
        {
            return Task.FromResult(PushChallengeDispatchResult.Failure("push_token_missing", isRetryable: false));
        }

        _logger.LogInformation(
            "Queued push challenge delivery dispatched. ChallengeId={ChallengeId} DeviceId={DeviceId} DeliveryId={DeliveryId} CorrelationId={CorrelationId}",
            request.ChallengeId,
            request.TargetDeviceId,
            request.DeliveryId,
            request.CorrelationId ?? "n/a");

        return Task.FromResult(PushChallengeDispatchResult.Success($"log:{request.DeliveryId}"));
    }
}
