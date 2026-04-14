using OtpAuth.Application.Policy;
using OtpAuth.Application.Integrations;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Challenges;

public sealed class CreateChallengeHandler
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly IChallengeRepository _challengeRepository;
    private readonly IPolicyEvaluator _policyEvaluator;

    public CreateChallengeHandler(
        IChallengeRepository challengeRepository,
        IPolicyEvaluator policyEvaluator)
    {
        _challengeRepository = challengeRepository;
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

        var availableFactors = preferredFactors
            .Append(FactorType.Totp)
            .Distinct()
            .ToArray();

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
            DeviceTrustState = DeviceTrustState.None,
            DeploymentProfile = DeploymentProfile.Cloud,
            EnvironmentMode = EnvironmentMode.Production,
            ChallengePurpose = MapChallengePurpose(request.OperationType),
            EnrollmentInitiationSource = EnrollmentInitiationSource.Admin,
            PushChannelAvailable = false,
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
            CorrelationId = NormalizeOptional(request.CorrelationId) ?? Guid.NewGuid().ToString("N"),
            CallbackUrl = request.CallbackUrl,
        };

        await _challengeRepository.AddAsync(challenge, cancellationToken);

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
}
