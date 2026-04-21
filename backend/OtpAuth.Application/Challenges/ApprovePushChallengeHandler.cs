using OtpAuth.Application.Devices;
using OtpAuth.Application.Policy;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Devices;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Challenges;

public sealed class ApprovePushChallengeHandler
{
    private readonly IChallengeDecisionAuditWriter _auditWriter;
    private readonly IChallengeAttemptRecorder _challengeAttemptRecorder;
    private readonly IChallengeRepository _challengeRepository;
    private readonly IDeviceRegistryStore _deviceRegistryStore;
    private readonly IPolicyEvaluator _policyEvaluator;

    public ApprovePushChallengeHandler(
        IChallengeRepository challengeRepository,
        IChallengeAttemptRecorder challengeAttemptRecorder,
        IChallengeDecisionAuditWriter auditWriter,
        IDeviceRegistryStore deviceRegistryStore,
        IPolicyEvaluator policyEvaluator)
    {
        _challengeRepository = challengeRepository;
        _challengeAttemptRecorder = challengeAttemptRecorder;
        _auditWriter = auditWriter;
        _deviceRegistryStore = deviceRegistryStore;
        _policyEvaluator = policyEvaluator;
    }

    public async Task<ApprovePushChallengeResult> HandleAsync(
        ApprovePushChallengeRequest request,
        DeviceClientContext deviceContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request, deviceContext);
        if (validationError is not null)
        {
            return ApprovePushChallengeResult.Failure(ApprovePushChallengeErrorCode.ValidationFailed, validationError);
        }

        if (!deviceContext.HasScope(DeviceTokenScope.Challenge))
        {
            return ApprovePushChallengeResult.Failure(
                ApprovePushChallengeErrorCode.PolicyDenied,
                $"Scope '{DeviceTokenScope.Challenge}' is required.");
        }

        var challenge = await _challengeRepository.GetByIdAsync(
            request.ChallengeId,
            deviceContext.TenantId,
            deviceContext.ApplicationClientId,
            cancellationToken);
        if (challenge is null)
        {
            return ApprovePushChallengeResult.Failure(
                ApprovePushChallengeErrorCode.NotFound,
                $"Challenge '{request.ChallengeId}' was not found.");
        }

        if (challenge.FactorType != FactorType.Push)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptTypes.PushApprove, ChallengeAttemptResults.UnsupportedFactor, cancellationToken);
            return ApprovePushChallengeResult.Failure(
                ApprovePushChallengeErrorCode.InvalidState,
                $"Challenge '{request.ChallengeId}' does not support push approval.",
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
            return ApprovePushChallengeResult.Failure(
                ApprovePushChallengeErrorCode.NotFound,
                $"Challenge '{request.ChallengeId}' was not found.");
        }

        if (challenge.Status != ChallengeStatus.Pending)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptTypes.PushApprove, ChallengeAttemptResults.InvalidState, cancellationToken);
            return ApprovePushChallengeResult.Failure(
                ApprovePushChallengeErrorCode.InvalidState,
                $"Challenge '{request.ChallengeId}' is not pending.",
                challenge);
        }

        if (challenge.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            var expiredAtUtc = DateTimeOffset.UtcNow;
            var expiredChallenge = challenge.MarkExpired();
            await _challengeRepository.UpdateAsync(
                expiredChallenge,
                ChallengeUpdateSideEffects.CreateForTerminalState(expiredChallenge, expiredAtUtc),
                cancellationToken);
            await RecordAttemptAsync(expiredChallenge.Id, ChallengeAttemptTypes.PushApprove, ChallengeAttemptResults.Expired, cancellationToken);

            return ApprovePushChallengeResult.Failure(
                ApprovePushChallengeErrorCode.ChallengeExpired,
                "Challenge has expired.",
                expiredChallenge);
        }

        var policyDecision = _policyEvaluator.Evaluate(new PolicyContext
        {
            TenantId = challenge.TenantId,
            ApplicationClientId = challenge.ApplicationClientId,
            OperationType = challenge.OperationType,
            UserId = CreateDeterministicUserId(challenge.ExternalUserId),
            UserStatus = UserStatus.Active,
            RequestedFactor = FactorType.Push,
            AvailableFactors = [FactorType.Push],
            DeviceTrustState = MapDeviceTrustState(device.Status),
            DeploymentProfile = DeploymentProfile.Cloud,
            EnvironmentMode = EnvironmentMode.Production,
            ChallengePurpose = MapChallengePurpose(challenge.OperationType),
            EnrollmentInitiationSource = EnrollmentInitiationSource.Admin,
            PushChannelAvailable = !string.IsNullOrWhiteSpace(device.PushToken),
        });

        if (!policyDecision.PushAllowed || policyDecision.IsDenied)
        {
            await RecordAttemptAsync(challenge.Id, ChallengeAttemptTypes.PushApprove, ChallengeAttemptResults.PolicyDenied, cancellationToken);
            return ApprovePushChallengeResult.Failure(
                ApprovePushChallengeErrorCode.PolicyDenied,
                policyDecision.DenyReason ?? "Push approval is not allowed for the current device.",
                challenge);
        }

        var approvedAtUtc = DateTimeOffset.UtcNow;
        var approvedChallenge = challenge.MarkApproved(approvedAtUtc);
        await _challengeRepository.UpdateAsync(
            approvedChallenge,
            ChallengeUpdateSideEffects.CreateForTerminalState(approvedChallenge, approvedAtUtc),
            cancellationToken);
        await RecordAttemptAsync(approvedChallenge.Id, ChallengeAttemptTypes.PushApprove, ChallengeAttemptResults.Approved, cancellationToken);
        await _auditWriter.WriteApprovedAsync(approvedChallenge, device, request.BiometricVerified, cancellationToken);

        return ApprovePushChallengeResult.Success(approvedChallenge);
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

    private static string? Validate(ApprovePushChallengeRequest request, DeviceClientContext deviceContext)
    {
        if (request.ChallengeId == Guid.Empty)
        {
            return "ChallengeId is required.";
        }

        if (request.DeviceId == Guid.Empty)
        {
            return "DeviceId is required.";
        }

        if (request.DeviceId != deviceContext.DeviceId)
        {
            return "DeviceId must match the authenticated device.";
        }

        if (!request.BiometricVerified)
        {
            return "BiometricVerified must be true for push approval.";
        }

        return null;
    }

    private static DeviceTrustState MapDeviceTrustState(DeviceStatus status)
    {
        return status switch
        {
            DeviceStatus.Pending => DeviceTrustState.Pending,
            DeviceStatus.Active => DeviceTrustState.Active,
            DeviceStatus.Revoked => DeviceTrustState.Revoked,
            DeviceStatus.Blocked => DeviceTrustState.Blocked,
            _ => DeviceTrustState.Unknown,
        };
    }

    private static ChallengePurpose MapChallengePurpose(OperationType operationType)
    {
        return operationType switch
        {
            OperationType.Login => ChallengePurpose.Authentication,
            OperationType.StepUp => ChallengePurpose.StepUp,
            OperationType.BackupCodeRecovery => ChallengePurpose.Recovery,
            _ => ChallengePurpose.Unknown,
        };
    }

    private static Guid CreateDeterministicUserId(string externalUserId)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(externalUserId.Trim());
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
