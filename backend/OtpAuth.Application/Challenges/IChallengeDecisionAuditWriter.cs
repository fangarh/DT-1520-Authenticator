using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Application.Challenges;

public interface IChallengeDecisionAuditWriter
{
    Task WriteApprovedAsync(
        Challenge challenge,
        RegisteredDevice device,
        bool biometricVerified,
        CancellationToken cancellationToken);

    Task WriteDeniedAsync(
        Challenge challenge,
        RegisteredDevice device,
        bool hasReason,
        CancellationToken cancellationToken);
}
