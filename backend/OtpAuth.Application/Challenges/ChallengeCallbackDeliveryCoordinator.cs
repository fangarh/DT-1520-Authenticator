using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Challenges;

public sealed record ChallengeCallbackDeliveryBatchResult
{
    public required int LeasedCount { get; init; }

    public required int DeliveredCount { get; init; }

    public required int RescheduledCount { get; init; }

    public required int FailedCount { get; init; }
}

public sealed class ChallengeCallbackDeliveryCoordinator
{
    private readonly IChallengeRepository _challengeRepository;
    private readonly IChallengeCallbackDeliveryGateway _gateway;
    private readonly IChallengeCallbackDeliveryStore _store;

    public ChallengeCallbackDeliveryCoordinator(
        IChallengeRepository challengeRepository,
        IChallengeCallbackDeliveryGateway gateway,
        IChallengeCallbackDeliveryStore store)
    {
        _challengeRepository = challengeRepository;
        _gateway = gateway;
        _store = store;
    }

    public async Task<ChallengeCallbackDeliveryBatchResult> DeliverDueAsync(
        DateTimeOffset utcNow,
        int batchSize,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var leasedDeliveries = await _store.LeaseDueAsync(
            utcNow,
            batchSize,
            leaseDuration,
            cancellationToken);
        var deliveredCount = 0;
        var rescheduledCount = 0;
        var failedCount = 0;

        foreach (var delivery in leasedDeliveries)
        {
            var challenge = await _challengeRepository.GetByIdAsync(
                delivery.ChallengeId,
                delivery.TenantId,
                delivery.ApplicationClientId,
                cancellationToken);
            if (challenge is null || !CanDeliver(challenge, delivery))
            {
                await _store.MarkFailedAsync(delivery.DeliveryId, "challenge_invalid", cancellationToken);
                failedCount++;
                continue;
            }

            var dispatchResult = await _gateway.DeliverAsync(
                new ChallengeCallbackDispatchRequest
                {
                    DeliveryId = delivery.DeliveryId,
                    ChallengeId = challenge.Id,
                    CallbackUrl = delivery.CallbackUrl,
                    EventType = delivery.EventType,
                    OccurredAtUtc = delivery.OccurredAtUtc,
                    Challenge = challenge,
                },
                cancellationToken);

            if (dispatchResult.IsSuccess)
            {
                await _store.MarkDeliveredAsync(delivery.DeliveryId, utcNow, cancellationToken);
                deliveredCount++;
                continue;
            }

            var attemptCount = delivery.AttemptCount;
            if (dispatchResult.IsRetryable && attemptCount < maxAttempts)
            {
                await _store.RescheduleAsync(
                    delivery.DeliveryId,
                    utcNow.Add(retryDelay),
                    dispatchResult.ErrorCode ?? "delivery_failed",
                    cancellationToken);
                rescheduledCount++;
                continue;
            }

            await _store.MarkFailedAsync(
                delivery.DeliveryId,
                dispatchResult.ErrorCode ?? "delivery_failed",
                cancellationToken);
            failedCount++;
        }

        return new ChallengeCallbackDeliveryBatchResult
        {
            LeasedCount = leasedDeliveries.Count,
            DeliveredCount = deliveredCount,
            RescheduledCount = rescheduledCount,
            FailedCount = failedCount,
        };
    }

    private static bool CanDeliver(Challenge challenge, ChallengeCallbackDelivery delivery)
    {
        return challenge.CallbackUrl is not null &&
               Uri.Compare(challenge.CallbackUrl, delivery.CallbackUrl, UriComponents.AbsoluteUri, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0 &&
               challenge.Status == MapStatus(delivery.EventType);
    }

    private static ChallengeStatus MapStatus(ChallengeCallbackEventType eventType)
    {
        return eventType switch
        {
            ChallengeCallbackEventType.Approved => ChallengeStatus.Approved,
            ChallengeCallbackEventType.Denied => ChallengeStatus.Denied,
            ChallengeCallbackEventType.Expired => ChallengeStatus.Expired,
            _ => throw new InvalidOperationException($"Unsupported challenge callback event type '{eventType}'."),
        };
    }
}
