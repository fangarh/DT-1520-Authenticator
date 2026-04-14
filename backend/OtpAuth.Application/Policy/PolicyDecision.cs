using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Policy;

public sealed record PolicyDecision
{
    public bool RequiresSecondFactor { get; init; }

    public IReadOnlyCollection<FactorType> AllowedFactors { get; init; } = Array.Empty<FactorType>();

    public FactorType? PreferredFactor { get; init; }

    public bool PushAllowed { get; init; }

    public bool TotpAllowed { get; init; }

    public bool BackupCodeAllowed { get; init; }

    public bool EnrollmentAllowed { get; init; }

    public string? DenyReason { get; init; }

    public string AuditReason { get; init; } = string.Empty;

    public IReadOnlyCollection<string> EvaluationTrace { get; init; } = Array.Empty<string>();

    public bool IsDenied => !string.IsNullOrWhiteSpace(DenyReason);
}
