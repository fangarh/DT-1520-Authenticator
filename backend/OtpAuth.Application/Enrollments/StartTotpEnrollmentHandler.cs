using System.Security.Cryptography;
using OtpAuth.Application.Integrations;
using OtpAuth.Application.Policy;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Enrollments;

public sealed class StartTotpEnrollmentHandler
{
    private const int TotpDigits = 6;
    private const int TotpPeriodSeconds = 30;
    private const int TotpSecretBytes = 20;
    private const string TotpAlgorithm = "SHA1";
    private const string DefaultIssuer = "OTPAuth";

    private readonly ITotpEnrollmentProvisioningStore _provisioningStore;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly ITotpEnrollmentAuditWriter _auditWriter;

    public StartTotpEnrollmentHandler(
        ITotpEnrollmentProvisioningStore provisioningStore,
        IPolicyEvaluator policyEvaluator,
        ITotpEnrollmentAuditWriter auditWriter)
    {
        _provisioningStore = provisioningStore;
        _policyEvaluator = policyEvaluator;
        _auditWriter = auditWriter;
    }

    public async Task<StartTotpEnrollmentResult> HandleAsync(
        StartTotpEnrollmentRequest request,
        IntegrationClientContext clientContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return StartTotpEnrollmentResult.Failure(StartTotpEnrollmentErrorCode.ValidationFailed, validationError);
        }

        var accessError = ValidateAccess(request, clientContext);
        if (accessError is not null)
        {
            return StartTotpEnrollmentResult.Failure(StartTotpEnrollmentErrorCode.AccessDenied, accessError);
        }

        var policyDecision = _policyEvaluator.Evaluate(CreatePolicyContext(request, clientContext));
        if (!policyDecision.EnrollmentAllowed)
        {
            return StartTotpEnrollmentResult.Failure(
                StartTotpEnrollmentErrorCode.PolicyDenied,
                policyDecision.DenyReason ?? "TOTP enrollment was denied by policy.");
        }

        var existingEnrollment = await _provisioningStore.GetByExternalUserIdAsync(
            request.TenantId,
            clientContext.ApplicationClientId,
            request.ExternalUserId,
            cancellationToken);
        if (existingEnrollment is { IsActive: true, ConfirmedUtc: not null })
        {
            return StartTotpEnrollmentResult.Failure(
                StartTotpEnrollmentErrorCode.Conflict,
                "An active TOTP enrollment already exists for the subject.");
        }

        var normalizedIssuer = NormalizeOptional(request.Issuer) ?? DefaultIssuer;
        var normalizedLabel = NormalizeOptional(request.Label) ?? request.ExternalUserId.Trim();
        var secret = RandomNumberGenerator.GetBytes(TotpSecretBytes);

        var pendingEnrollment = await _provisioningStore.UpsertPendingAsync(
            new TotpEnrollmentProvisioningDraft
            {
                TenantId = request.TenantId,
                ApplicationClientId = clientContext.ApplicationClientId,
                ExternalUserId = request.ExternalUserId.Trim(),
                Label = normalizedLabel,
                Secret = secret,
                Digits = TotpDigits,
                PeriodSeconds = TotpPeriodSeconds,
                Algorithm = TotpAlgorithm,
            },
            cancellationToken);

        var secretUri = TotpProvisioningUriBuilder.Build(
            normalizedIssuer,
            normalizedLabel,
            pendingEnrollment.Secret,
            pendingEnrollment.Digits,
            pendingEnrollment.PeriodSeconds,
            pendingEnrollment.Algorithm);
        var response = new TotpEnrollmentView
        {
            EnrollmentId = pendingEnrollment.EnrollmentId,
            Status = TotpEnrollmentStatus.Pending,
            HasPendingReplacement = false,
            SecretUri = secretUri,
            QrCodePayload = secretUri,
        };

        await _auditWriter.WriteStartedAsync(
            response,
            request.TenantId,
            clientContext.ApplicationClientId,
            request.ExternalUserId.Trim(),
            normalizedLabel,
            normalizedIssuer,
            cancellationToken);

        return StartTotpEnrollmentResult.Success(response);
    }

    private static string? Validate(StartTotpEnrollmentRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ExternalUserId))
        {
            return "ExternalUserId is required.";
        }

        if (NormalizeOptional(request.ExternalUserId)?.Length > 256)
        {
            return "ExternalUserId must be 256 characters or fewer.";
        }

        if (NormalizeOptional(request.Issuer)?.Length > 128)
        {
            return "Issuer must be 128 characters or fewer.";
        }

        if (NormalizeOptional(request.Label)?.Length > 256)
        {
            return "Label must be 256 characters or fewer.";
        }

        return null;
    }

    private static string? ValidateAccess(StartTotpEnrollmentRequest request, IntegrationClientContext clientContext)
    {
        if (!clientContext.HasScope(IntegrationClientScopes.EnrollmentsWrite))
        {
            return $"Scope '{IntegrationClientScopes.EnrollmentsWrite}' is required.";
        }

        if (request.TenantId != clientContext.TenantId)
        {
            return "Request tenant is outside the authenticated client scope.";
        }

        return null;
    }

    private static PolicyContext CreatePolicyContext(
        StartTotpEnrollmentRequest request,
        IntegrationClientContext clientContext)
    {
        return new PolicyContext
        {
            TenantId = request.TenantId,
            ApplicationClientId = clientContext.ApplicationClientId,
            OperationType = OperationType.TotpEnrollment,
            UserId = CreateDeterministicUserId(request.ExternalUserId),
            UserStatus = UserStatus.Active,
            RequestedFactor = FactorType.Totp,
            AvailableFactors = [FactorType.Totp],
            DeviceTrustState = DeviceTrustState.None,
            DeploymentProfile = DeploymentProfile.Cloud,
            EnvironmentMode = EnvironmentMode.Production,
            ChallengePurpose = ChallengePurpose.Enrollment,
            EnrollmentInitiationSource = EnrollmentInitiationSource.TrustedIntegration,
            PushChannelAvailable = false,
        };
    }

    private static Guid CreateDeterministicUserId(string externalUserId)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(externalUserId.Trim());
        var hash = SHA256.HashData(bytes);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
