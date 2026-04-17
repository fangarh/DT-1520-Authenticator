using OtpAuth.Application.Policy;
using OtpAuth.Domain.Policy;

namespace OtpAuth.Infrastructure.Policy;

public sealed class DefaultPolicyEvaluator : IPolicyEvaluator
{
    public PolicyDecision Evaluate(PolicyContext context)
    {
        var trace = new List<string>();

        if (!context.IsComplete)
        {
            trace.Add("Denied: policy context is incomplete.");
            return CreateDeniedDecision(
                denyReason: "Policy context is incomplete.",
                auditReason: "policy.denied.incomplete_context",
                trace);
        }

        if (context.UserStatus != UserStatus.Active)
        {
            trace.Add($"Denied: user status is '{context.UserStatus}'.");
            return CreateDeniedDecision(
                denyReason: $"User status '{context.UserStatus}' is not allowed.",
                auditReason: "policy.denied.user_status",
                trace);
        }

        return context.OperationType switch
        {
            OperationType.DeviceActivation => EvaluateEnrollmentLikeContext(
                context,
                FactorType.Push,
                "policy.device_activation"),
            OperationType.TotpEnrollment => EvaluateEnrollmentLikeContext(
                context,
                FactorType.Totp,
                "policy.totp_enrollment"),
            _ => EvaluateChallengeContext(context, trace),
        };
    }

    private static PolicyDecision EvaluateEnrollmentLikeContext(
        PolicyContext context,
        FactorType expectedFactor,
        string auditReason)
    {
        var trace = new List<string>();
        var trustedInitiation =
            context.EnrollmentInitiationSource is EnrollmentInitiationSource.Admin or EnrollmentInitiationSource.TrustedIntegration;

        if (!trustedInitiation)
        {
            trace.Add($"Denied: enrollment initiation source is '{context.EnrollmentInitiationSource}'.");
            return CreateDeniedDecision(
                denyReason: "Enrollment initiation source is not trusted.",
                auditReason: $"{auditReason}.denied.source",
                trace,
                enrollmentAllowed: false);
        }

        if (!context.AvailableFactors.Contains(expectedFactor))
        {
            trace.Add($"Denied: expected factor '{expectedFactor}' is unavailable.");
            return CreateDeniedDecision(
                denyReason: $"Factor '{expectedFactor}' is unavailable.",
                auditReason: $"{auditReason}.denied.factor_unavailable",
                trace,
                enrollmentAllowed: false);
        }

        trace.Add($"Allowed: enrollment action for '{expectedFactor}'.");

        return new PolicyDecision
        {
            RequiresSecondFactor = false,
            AllowedFactors = [expectedFactor],
            PreferredFactor = expectedFactor,
            PushAllowed = expectedFactor == FactorType.Push,
            TotpAllowed = expectedFactor == FactorType.Totp,
            BackupCodeAllowed = false,
            EnrollmentAllowed = true,
            AuditReason = $"{auditReason}.allowed",
            EvaluationTrace = trace,
        };
    }

    private static PolicyDecision EvaluateChallengeContext(PolicyContext context, List<string> trace)
    {
        var availableFactors = context.AvailableFactors
            .Where(factor => factor != FactorType.Unknown)
            .Distinct()
            .ToHashSet();

        var pushAllowed =
            availableFactors.Contains(FactorType.Push) &&
            context.PushChannelAvailable &&
            context.DeviceTrustState == DeviceTrustState.Active &&
            context.DeploymentProfile != DeploymentProfile.AirGapped;

        if (pushAllowed)
        {
            trace.Add("Push allowed: channel available, device active, deployment profile permits push.");
        }
        else
        {
            trace.Add("Push denied: unavailable channel, inactive device, or restricted deployment profile.");
        }

        var totpAllowed = availableFactors.Contains(FactorType.Totp);
        var backupCodeAllowed =
            availableFactors.Contains(FactorType.BackupCode) &&
            context.OperationType != OperationType.StepUp;

        if (totpAllowed)
        {
            trace.Add("TOTP allowed.");
        }

        if (backupCodeAllowed)
        {
            trace.Add("Backup code allowed.");
        }

        var allowedFactors = new List<FactorType>();

        if (pushAllowed)
        {
            allowedFactors.Add(FactorType.Push);
        }

        if (totpAllowed)
        {
            allowedFactors.Add(FactorType.Totp);
        }

        if (backupCodeAllowed)
        {
            allowedFactors.Add(FactorType.BackupCode);
        }

        var requiresSecondFactor = context.OperationType is OperationType.Login or OperationType.StepUp;

        if (allowedFactors.Count == 0)
        {
            trace.Add("Denied: no factors allowed for the current context.");
            return CreateDeniedDecision(
                denyReason: "No factors allowed for the current context.",
                auditReason: "policy.denied.no_allowed_factors",
                trace,
                requiresSecondFactor: requiresSecondFactor,
                pushAllowed: pushAllowed,
                totpAllowed: totpAllowed,
                backupCodeAllowed: backupCodeAllowed);
        }

        string? denyReason = null;

        if (context.RequestedFactor is { } requestedFactor && !allowedFactors.Contains(requestedFactor))
        {
            trace.Add($"Denied requested factor '{requestedFactor}' because it is not allowed.");
            denyReason = $"Requested factor '{requestedFactor}' is not allowed.";
        }

        var preferredFactor = context.RequestedFactor is { } preferredRequestedFactor && allowedFactors.Contains(preferredRequestedFactor)
            ? preferredRequestedFactor
            : ResolvePreferredFactor(allowedFactors);

        return new PolicyDecision
        {
            RequiresSecondFactor = requiresSecondFactor,
            AllowedFactors = allowedFactors,
            PreferredFactor = preferredFactor,
            PushAllowed = pushAllowed,
            TotpAllowed = totpAllowed,
            BackupCodeAllowed = backupCodeAllowed,
            EnrollmentAllowed = false,
            DenyReason = denyReason,
            AuditReason = denyReason is null ? "policy.allowed" : "policy.denied.requested_factor",
            EvaluationTrace = trace,
        };
    }

    private static PolicyDecision CreateDeniedDecision(
        string denyReason,
        string auditReason,
        IReadOnlyCollection<string> trace,
        bool requiresSecondFactor = false,
        bool pushAllowed = false,
        bool totpAllowed = false,
        bool backupCodeAllowed = false,
        bool enrollmentAllowed = false)
    {
        return new PolicyDecision
        {
            RequiresSecondFactor = requiresSecondFactor,
            AllowedFactors = Array.Empty<FactorType>(),
            PreferredFactor = null,
            PushAllowed = pushAllowed,
            TotpAllowed = totpAllowed,
            BackupCodeAllowed = backupCodeAllowed,
            EnrollmentAllowed = enrollmentAllowed,
            DenyReason = denyReason,
            AuditReason = auditReason,
            EvaluationTrace = trace,
        };
    }

    private static FactorType? ResolvePreferredFactor(IReadOnlyCollection<FactorType> allowedFactors)
    {
        if (allowedFactors.Contains(FactorType.Push))
        {
            return FactorType.Push;
        }

        if (allowedFactors.Contains(FactorType.Totp))
        {
            return FactorType.Totp;
        }

        if (allowedFactors.Contains(FactorType.BackupCode))
        {
            return FactorType.BackupCode;
        }

        return null;
    }
}
