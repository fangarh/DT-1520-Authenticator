using OtpAuth.Application.Devices;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Devices;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Challenges;

public sealed record PushChallengeDeliveryBatchResult
{
    public required int LeasedCount { get; init; }

    public required int DeliveredCount { get; init; }

    public required int RescheduledCount { get; init; }

    public required int FailedCount { get; init; }
}

public sealed class PushChallengeDeliveryCoordinator
{
    private readonly IChallengeRepository _challengeRepository;
    private readonly IDeviceRegistryStore _deviceRegistryStore;
    private readonly IPushChallengeDeliveryGateway _gateway;
    private readonly IPushChallengeDeliveryStore _deliveryStore;

    public PushChallengeDeliveryCoordinator(
        IChallengeRepository challengeRepository,
        IDeviceRegistryStore deviceRegistryStore,
        IPushChallengeDeliveryGateway gateway,
        IPushChallengeDeliveryStore deliveryStore)
    {
        _challengeRepository = challengeRepository;
        _deviceRegistryStore = deviceRegistryStore;
        _gateway = gateway;
        _deliveryStore = deliveryStore;
    }

    public async Task<PushChallengeDeliveryBatchResult> DeliverDueAsync(
        DateTimeOffset utcNow,
        int batchSize,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var leasedDeliveries = await _deliveryStore.LeaseDueAsync(
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
            if (challenge is null || !CanDeliver(challenge, delivery, utcNow))
            {
                await _deliveryStore.MarkFailedAsync(delivery.DeliveryId, "challenge_invalid", cancellationToken);
                failedCount++;
                continue;
            }

            var device = await _deviceRegistryStore.GetByIdAsync(
                delivery.TargetDeviceId,
                delivery.TenantId,
                delivery.ApplicationClientId,
                cancellationToken);
            if (device is null || !CanDeliver(device, challenge))
            {
                await _deliveryStore.MarkFailedAsync(delivery.DeliveryId, "device_unavailable", cancellationToken);
                failedCount++;
                continue;
            }

            var dispatchResult = await _gateway.DeliverAsync(
                new PushChallengeDispatchRequest
                {
                    DeliveryId = delivery.DeliveryId,
                    ChallengeId = challenge.Id,
                    TargetDeviceId = device.Id,
                    PushToken = device.PushToken!,
                    ExternalUserId = challenge.ExternalUserId,
                    OperationType = challenge.OperationType.ToString(),
                    OperationDisplayName = challenge.OperationDisplayName,
                    CorrelationId = challenge.CorrelationId,
                },
                cancellationToken);

            if (dispatchResult.IsSuccess)
            {
                await _deliveryStore.MarkDeliveredAsync(
                    delivery.DeliveryId,
                    utcNow,
                    dispatchResult.ProviderMessageId,
                    cancellationToken);
                deliveredCount++;
                continue;
            }

            var attemptCount = delivery.AttemptCount;
            if (dispatchResult.IsRetryable && attemptCount < maxAttempts)
            {
                await _deliveryStore.RescheduleAsync(
                    delivery.DeliveryId,
                    utcNow.Add(retryDelay),
                    dispatchResult.ErrorCode ?? "delivery_failed",
                    cancellationToken);
                rescheduledCount++;
                continue;
            }

            await _deliveryStore.MarkFailedAsync(
                delivery.DeliveryId,
                dispatchResult.ErrorCode ?? "delivery_failed",
                cancellationToken);
            failedCount++;
        }

        return new PushChallengeDeliveryBatchResult
        {
            LeasedCount = leasedDeliveries.Count,
            DeliveredCount = deliveredCount,
            RescheduledCount = rescheduledCount,
            FailedCount = failedCount,
        };
    }

    private static bool CanDeliver(Challenge challenge, PushChallengeDelivery delivery, DateTimeOffset utcNow)
    {
        return challenge.FactorType == FactorType.Push &&
               challenge.Status == ChallengeStatus.Pending &&
               challenge.TargetDeviceId == delivery.TargetDeviceId &&
               challenge.ExpiresAt > utcNow;
    }

    private static bool CanDeliver(RegisteredDevice device, Challenge challenge)
    {
        return device.Status == DeviceStatus.Active &&
               !string.IsNullOrWhiteSpace(device.PushToken) &&
               string.Equals(device.ExternalUserId, challenge.ExternalUserId, StringComparison.Ordinal);
    }
}
