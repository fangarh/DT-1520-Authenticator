using OtpAuth.Application.Devices;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Challenges;

public sealed class DenyPushChallengeHandler
{
    private readonly IChallengeDecisionAuditWriter _auditWriter;
    private readonly IChallengeAttemptRecorder _challengeAttemptRecorder;
    private readonly IChallengeRepository _challengeRepository;
    private readonly IDeviceRegistryStore _deviceRegistryStore;

    public DenyPushChallengeHandler(
        IChallengeRepository challengeRepository,
        IChallengeAttemptRecorder challengeAttemptRecorder,
        IChallengeDecisionAuditWriter auditWriter,
        IDeviceRegistryStore deviceRegistryStore)
    {
        _challengeRepository = challengeRepository;
        _challengeAttemptRecorder = challengeAttemptRecorder;
        _auditWriter = auditWriter;
        _deviceRegistryStore = deviceRegistryStore;
    }

    public async Task<DenyPushChallengeResult> HandleAsync(
        DenyPushChallengeRequest request,
        DeviceClientContext deviceContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request, deviceContext);
        if (validationError is not null)
        {
            return DenyPushChallengeResult.Failure(DenyPushChallengeErrorCode.ValidationFailed, validationError);
        }

        if (!deviceContext.HasScope(DeviceTokenScope.Challenge))
        {
            return DenyPushChallengeResult.Failure(
                DenyPushChallengeErrorCode.InvalidState,
                $"Scope '{DeviceTokenScope.Challenge}' is required.");
        }

        var challenge = await _challengeRepository.GetByIdAsync(
            request.ChallengeId,
            deviceContext.TenantId,
            deviceContext.ApplicationClientId,
            cancellationToken);
        if (challenge is null)
        {
            return DenyPushChallengeResult.Failure(
                DenyPushChallengeErrorCode.NotFound,
                $"Challenge '{request.ChallengeId}' was not found.");
        }

        if (challenge.FactorType != FactorType.Push)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptTypes.PushDeny, ChallengeAttemptResults.UnsupportedFactor, cancellationToken);
            return DenyPushChallengeResult.Failure(
                DenyPushChallengeErrorCode.InvalidState,
                $"Challenge '{request.ChallengeId}' does not support push denial.",
                challenge);
        }

        var device = await _deviceRegistryStore.GetByIdAsync(
            deviceContext.DeviceId,
            deviceContext.TenantId,
            deviceContext.ApplicationClientId,
            cancellationToken);
        if (device is null ||
            challenge.TargetDeviceId != device.Id ||
            !string.Equals(device.ExternalUserId, challenge.ExternalUserId, StringComparison.Ordinal))
        {
            return DenyPushChallengeResult.Failure(
                DenyPushChallengeErrorCode.NotFound,
                $"Challenge '{request.ChallengeId}' was not found.");
        }

        if (challenge.Status != ChallengeStatus.Pending)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptTypes.PushDeny, ChallengeAttemptResults.InvalidState, cancellationToken);
            return DenyPushChallengeResult.Failure(
                DenyPushChallengeErrorCode.InvalidState,
                $"Challenge '{request.ChallengeId}' is not pending.",
                challenge);
        }

        if (challenge.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            var expiredChallenge = challenge.MarkExpired();
            await _challengeRepository.UpdateAsync(expiredChallenge, cancellationToken);
            await RecordAttemptAsync(expiredChallenge.Id, ChallengeAttemptTypes.PushDeny, ChallengeAttemptResults.Expired, cancellationToken);

            return DenyPushChallengeResult.Failure(
                DenyPushChallengeErrorCode.ChallengeExpired,
                "Challenge has expired.",
                expiredChallenge);
        }

        var deniedChallenge = challenge.MarkDenied(DateTimeOffset.UtcNow);
        await _challengeRepository.UpdateAsync(deniedChallenge, cancellationToken);
        await RecordAttemptAsync(deniedChallenge.Id, ChallengeAttemptTypes.PushDeny, ChallengeAttemptResults.Denied, cancellationToken);
        await _auditWriter.WriteDeniedAsync(
            deniedChallenge,
            device,
            !string.IsNullOrWhiteSpace(request.Reason),
            cancellationToken);

        return DenyPushChallengeResult.Success(deniedChallenge);
    }

    private Task RecordAttemptAsync(
        Guid challengeId,
        string attemptType,
        string result,
        CancellationToken cancellationToken)
    {
        return _challengeAttemptRecorder.RecordAsync(
            new ChallengeAttemptRecord
            {
                ChallengeId = challengeId,
                AttemptType = attemptType,
                Result = result,
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private static string? Validate(DenyPushChallengeRequest request, DeviceClientContext deviceContext)
    {
        if (request.ChallengeId == Guid.Empty)
        {
            return "ChallengeId is required.";
        }

        if (request.DeviceId.HasValue && request.DeviceId.Value != deviceContext.DeviceId)
        {
            return "DeviceId must match the authenticated device when provided.";
        }

        if (request.Reason is { Length: > 256 })
        {
            return "Reason must be 256 characters or fewer.";
        }

        return null;
    }
}
