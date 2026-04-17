using OtpAuth.Application.Policy;
using OtpAuth.Application.Devices;
using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Challenges;

public sealed class CreateChallengeHandler
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly IChallengeRepository _challengeRepository;
    private readonly IDeviceRegistryStore _deviceRegistryStore;
    private readonly IPolicyEvaluator _policyEvaluator;

    public CreateChallengeHandler(
        IChallengeRepository challengeRepository,
        IDeviceRegistryStore deviceRegistryStore,
        IPolicyEvaluator policyEvaluator)
    {
        _challengeRepository = challengeRepository;
        _deviceRegistryStore = deviceRegistryStore;
        _policyEvaluator = policyEvaluator;
    }

    public async Task<CreateChallengeResult> HandleAsync(
        CreateChallengeRequest request,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return CreateChallengeResult.Failure(CreateChallengeErrorCode.ValidationFailed, validationError);
        }

        var accessError = ValidateAccess(request, clientContext);
        if (accessError is not null)
        {
            return CreateChallengeResult.Failure(CreateChallengeErrorCode.AccessDenied, accessError);
        }

        var preferredFactors = request.PreferredFactors
            .Where(factor => factor != FactorType.Unknown)
            .Distinct()
            .ToArray();

        if (request.TargetDeviceId.HasValue && !preferredFactors.Contains(FactorType.Push))
        {
            return CreateChallengeResult.Failure(
                CreateChallengeErrorCode.ValidationFailed,
                "TargetDeviceId can be used only when preferredFactors include push.");
        }

        var availableFactors = preferredFactors
            .Append(FactorType.Totp)
            .Distinct()
            .ToArray();

        var pushResolution = await ResolvePushDeviceAsync(
            request,
            preferredFactors,
            cancellationToken);
        if (pushResolution.ErrorMessage is not null)
        {
            return CreateChallengeResult.Failure(CreateChallengeErrorCode.ValidationFailed, pushResolution.ErrorMessage);
        }

        var resolvedPushDevice = pushResolution.Device;

        var policyContext = new PolicyContext
        {
            TenantId = request.TenantId,
            ApplicationClientId = request.ApplicationClientId,
            OperationType = request.OperationType,
            UserId = CreateDeterministicUserId(request.ExternalUserId),
            UserStatus = UserStatus.Active,
            RequestedFactor = preferredFactors.Length == 1
                ? preferredFactors[0]
                : null,
            AvailableFactors = availableFactors,
            DeviceTrustState = resolvedPushDevice is null
                ? DeviceTrustState.None
                : MapDeviceTrustState(resolvedPushDevice.Status),
            DeploymentProfile = DeploymentProfile.Cloud,
            EnvironmentMode = EnvironmentMode.Production,
            ChallengePurpose = MapChallengePurpose(request.OperationType),
            EnrollmentInitiationSource = EnrollmentInitiationSource.Admin,
            PushChannelAvailable = !string.IsNullOrWhiteSpace(resolvedPushDevice?.PushToken),
        };

        var policyDecision = _policyEvaluator.Evaluate(policyContext);
        if (policyDecision.IsDenied || policyDecision.PreferredFactor is null)
        {
            return CreateChallengeResult.Failure(
                CreateChallengeErrorCode.PolicyDenied,
                policyDecision.DenyReason ?? "Challenge creation was denied by policy.");
        }

        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ApplicationClientId = request.ApplicationClientId,
            ExternalUserId = request.ExternalUserId.Trim(),
            Username = NormalizeOptional(request.Username),
            OperationType = request.OperationType,
            OperationDisplayName = NormalizeOptional(request.OperationDisplayName),
            FactorType = policyDecision.PreferredFactor.Value,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ChallengeLifetime),
            TargetDeviceId = policyDecision.PreferredFactor == FactorType.Push
                ? resolvedPushDevice?.Id
                : null,
            CorrelationId = NormalizeOptional(request.CorrelationId) ?? Guid.NewGuid().ToString("N"),
            CallbackUrl = request.CallbackUrl,
        };

        var pushDelivery = challenge.FactorType == FactorType.Push && challenge.TargetDeviceId.HasValue
            ? PushChallengeDelivery.CreateQueued(
                challenge.Id,
                challenge.TenantId,
                challenge.ApplicationClientId,
                challenge.ExternalUserId,
                challenge.TargetDeviceId.Value,
                DateTimeOffset.UtcNow)
            : null;

        await _challengeRepository.AddAsync(challenge, pushDelivery, cancellationToken);

        return CreateChallengeResult.Success(challenge);
    }

    private static string? Validate(CreateChallengeRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (request.ApplicationClientId == Guid.Empty)
        {
            return "ApplicationClientId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ExternalUserId))
        {
            return "ExternalUserId is required.";
        }

        if (request.OperationType is OperationType.Unknown or OperationType.DeviceActivation or OperationType.TotpEnrollment)
        {
            return $"OperationType '{request.OperationType}' is not supported for challenge creation.";
        }

        if (request.CallbackUrl is not null && request.CallbackUrl.Scheme != Uri.UriSchemeHttps)
        {
            return "CallbackUrl must use HTTPS.";
        }

        if (NormalizeOptional(request.CorrelationId)?.Length > 128)
        {
            return "CorrelationId must be 128 characters or fewer.";
        }

        return null;
    }

    private async Task<PushDeviceResolution> ResolvePushDeviceAsync(
        CreateChallengeRequest request,
        IReadOnlyCollection<FactorType> preferredFactors,
        CancellationToken cancellationToken)
    {
        if (!preferredFactors.Contains(FactorType.Push))
        {
            return PushDeviceResolution.None();
        }

        if (request.TargetDeviceId.HasValue)
        {
            var explicitTarget = await _deviceRegistryStore.GetByIdAsync(
                request.TargetDeviceId.Value,
                request.TenantId,
                request.ApplicationClientId,
                cancellationToken);
            if (explicitTarget is null ||
                explicitTarget.Status != Domain.Devices.DeviceStatus.Active ||
                string.IsNullOrWhiteSpace(explicitTarget.PushToken) ||
                !string.Equals(explicitTarget.ExternalUserId, request.ExternalUserId.Trim(), StringComparison.Ordinal))
            {
                return PushDeviceResolution.Invalid(
                    "TargetDeviceId must reference an active push-capable device bound to the requested user.");
            }

            return PushDeviceResolution.Success(explicitTarget);
        }

        var activeDevices = await _deviceRegistryStore.ListActiveByExternalUserAsync(
            request.TenantId,
            request.ApplicationClientId,
            request.ExternalUserId.Trim(),
            cancellationToken);
        var pushCapableDevices = activeDevices
            .Where(device => !string.IsNullOrWhiteSpace(device.PushToken))
            .ToArray();

        return pushCapableDevices.Length == 1
            ? PushDeviceResolution.Success(pushCapableDevices[0])
            : PushDeviceResolution.None();
    }

    private static DeviceTrustState MapDeviceTrustState(Domain.Devices.DeviceStatus status)
    {
        return status switch
        {
            Domain.Devices.DeviceStatus.Pending => DeviceTrustState.Pending,
            Domain.Devices.DeviceStatus.Active => DeviceTrustState.Active,
            Domain.Devices.DeviceStatus.Revoked => DeviceTrustState.Revoked,
            Domain.Devices.DeviceStatus.Blocked => DeviceTrustState.Blocked,
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ValidateAccess(CreateChallengeRequest request, IntegrationClientContext clientContext)
    {
        if (!clientContext.HasScope(IntegrationClientScopes.ChallengesWrite))
        {
            return $"Scope '{IntegrationClientScopes.ChallengesWrite}' is required.";
        }

        if (request.TenantId != clientContext.TenantId)
        {
            return "Request tenant is outside the authenticated client scope.";
        }

        if (request.ApplicationClientId != clientContext.ApplicationClientId)
        {
            return "Request application client is outside the authenticated client scope.";
        }

        return null;
    }

    private static Guid CreateDeterministicUserId(string externalUserId)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(externalUserId.Trim());
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    private sealed record PushDeviceResolution
    {
        public Domain.Devices.RegisteredDevice? Device { get; init; }

        public string? ErrorMessage { get; init; }

        public static PushDeviceResolution None() => new();

        public static PushDeviceResolution Success(Domain.Devices.RegisteredDevice device) => new()
        {
            Device = device,
        };

        public static PushDeviceResolution Invalid(string errorMessage) => new()
        {
            ErrorMessage = errorMessage,
        };
    }
}
