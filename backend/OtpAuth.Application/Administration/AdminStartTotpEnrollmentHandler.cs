using System.Security.Cryptography;
using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Policy;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Administration;

public sealed class AdminStartTotpEnrollmentHandler
{
    private const int TotpDigits = 6;
    private const int TotpPeriodSeconds = 30;
    private const int TotpSecretBytes = 20;
    private const string TotpAlgorithm = "SHA1";
    private const string DefaultIssuer = "OTPAuth";

    private readonly ITotpEnrollmentProvisioningStore _provisioningStore;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly ITotpEnrollmentAuditWriter _auditWriter;
    private readonly IAdminTotpEnrollmentAuditWriter _adminAuditWriter;
    private readonly IAdminApplicationClientResolver _applicationClientResolver;

    public AdminStartTotpEnrollmentHandler(
        ITotpEnrollmentProvisioningStore provisioningStore,
        IPolicyEvaluator policyEvaluator,
        ITotpEnrollmentAuditWriter auditWriter,
        IAdminTotpEnrollmentAuditWriter adminAuditWriter,
        IAdminApplicationClientResolver applicationClientResolver)
    {
        _provisioningStore = provisioningStore;
        _policyEvaluator = policyEvaluator;
        _auditWriter = auditWriter;
        _adminAuditWriter = adminAuditWriter;
        _applicationClientResolver = applicationClientResolver;
    }

    public async Task<AdminStartTotpEnrollmentResult> HandleAsync(
        AdminStartTotpEnrollmentRequest request,
        AdminContext adminContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return AdminStartTotpEnrollmentResult.Failure(AdminStartTotpEnrollmentErrorCode.ValidationFailed, validationError);
        }

        var accessError = ValidateAccess(adminContext);
        if (accessError is not null)
        {
            return AdminStartTotpEnrollmentResult.Failure(AdminStartTotpEnrollmentErrorCode.AccessDenied, accessError);
        }

        var applicationClientResolution = await _applicationClientResolver.ResolveAsync(
            request.TenantId,
            request.ApplicationClientId,
            cancellationToken);
        if (!applicationClientResolution.IsSuccess || applicationClientResolution.ApplicationClientId is not Guid applicationClientId)
        {
            return AdminStartTotpEnrollmentResult.Failure(
                applicationClientResolution.ErrorCode == AdminApplicationClientResolutionErrorCode.Conflict
                    ? AdminStartTotpEnrollmentErrorCode.Conflict
                    : AdminStartTotpEnrollmentErrorCode.NotFound,
                applicationClientResolution.ErrorMessage ?? "Application client resolution failed.");
        }

        var policyDecision = _policyEvaluator.Evaluate(CreatePolicyContext(request, applicationClientId));
        if (!policyDecision.EnrollmentAllowed)
        {
            return AdminStartTotpEnrollmentResult.Failure(
                AdminStartTotpEnrollmentErrorCode.PolicyDenied,
                policyDecision.DenyReason ?? "TOTP enrollment was denied by policy.");
        }

        var normalizedExternalUserId = request.ExternalUserId.Trim();
        var existingEnrollment = await _provisioningStore.GetByExternalUserIdAsync(
            request.TenantId,
            applicationClientId,
            normalizedExternalUserId,
            cancellationToken);
        if (existingEnrollment is { IsActive: true, ConfirmedUtc: not null })
        {
            return AdminStartTotpEnrollmentResult.Failure(
                AdminStartTotpEnrollmentErrorCode.Conflict,
                "An active TOTP enrollment already exists for the subject.");
        }

        var normalizedIssuer = NormalizeOptional(request.Issuer) ?? DefaultIssuer;
        var normalizedLabel = NormalizeOptional(request.Label) ?? normalizedExternalUserId;
        var secret = RandomNumberGenerator.GetBytes(TotpSecretBytes);

        var pendingEnrollment = await _provisioningStore.UpsertPendingAsync(
            new TotpEnrollmentProvisioningDraft
            {
                TenantId = request.TenantId,
                ApplicationClientId = applicationClientId,
                ExternalUserId = normalizedExternalUserId,
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
            applicationClientId,
            normalizedExternalUserId,
            normalizedLabel,
            normalizedIssuer,
            cancellationToken);
        await _adminAuditWriter.WriteStartedAsync(
            adminContext,
            response,
            request.TenantId,
            applicationClientId,
            normalizedExternalUserId,
            normalizedLabel,
            normalizedIssuer,
            cancellationToken);

        return AdminStartTotpEnrollmentResult.Success(response);
    }

    private static string? Validate(AdminStartTotpEnrollmentRequest request)
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

    private static string? ValidateAccess(AdminContext adminContext)
    {
        return adminContext.HasPermission(AdminPermissions.EnrollmentsWrite)
            ? null
            : $"Permission '{AdminPermissions.EnrollmentsWrite}' is required.";
    }

    private static PolicyContext CreatePolicyContext(
        AdminStartTotpEnrollmentRequest request,
        Guid applicationClientId)
    {
        return new PolicyContext
        {
            TenantId = request.TenantId,
            ApplicationClientId = applicationClientId,
            OperationType = OperationType.TotpEnrollment,
            UserId = CreateDeterministicUserId(request.ExternalUserId),
            UserStatus = UserStatus.Active,
            RequestedFactor = FactorType.Totp,
            AvailableFactors = [FactorType.Totp],
            DeviceTrustState = DeviceTrustState.None,
            DeploymentProfile = DeploymentProfile.Cloud,
            EnvironmentMode = EnvironmentMode.Production,
            ChallengePurpose = ChallengePurpose.Enrollment,
            EnrollmentInitiationSource = EnrollmentInitiationSource.Admin,
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
