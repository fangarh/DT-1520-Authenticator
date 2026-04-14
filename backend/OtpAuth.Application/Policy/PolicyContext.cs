using OtpAuth.Domain.Policy;

namespace OtpAuth.Application.Policy;

public sealed record PolicyContext
{
    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required OperationType OperationType { get; init; }

    public required Guid UserId { get; init; }

    public required UserStatus UserStatus { get; init; }

    public FactorType? RequestedFactor { get; init; }

    public IReadOnlyCollection<FactorType> AvailableFactors { get; init; } = Array.Empty<FactorType>();

    public required DeviceTrustState DeviceTrustState { get; init; }

    public required DeploymentProfile DeploymentProfile { get; init; }

    public required EnvironmentMode EnvironmentMode { get; init; }

    public required ChallengePurpose ChallengePurpose { get; init; }

    public required EnrollmentInitiationSource EnrollmentInitiationSource { get; init; }

    public required bool PushChannelAvailable { get; init; }

    public bool IsComplete =>
        TenantId != Guid.Empty &&
        ApplicationClientId != Guid.Empty &&
        UserId != Guid.Empty &&
        OperationType != OperationType.Unknown &&
        UserStatus != UserStatus.Unknown &&
        DeviceTrustState != DeviceTrustState.Unknown &&
        DeploymentProfile != DeploymentProfile.Unknown &&
        EnvironmentMode != EnvironmentMode.Unknown &&
        ChallengePurpose != ChallengePurpose.Unknown &&
        EnrollmentInitiationSource != EnrollmentInitiationSource.Unknown &&
        AvailableFactors.Count > 0;
}
