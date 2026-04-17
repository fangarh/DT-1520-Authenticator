using OtpAuth.Application.Challenges;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Infrastructure.Tests.Challenges;

internal sealed class RecordingChallengeAttemptRecorder : IChallengeAttemptRecorder
{
    public List<ChallengeAttemptRecord> Attempts { get; } = [];

    public Task RecordAsync(ChallengeAttemptRecord attempt, CancellationToken cancellationToken)
    {
        Attempts.Add(attempt);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingChallengeDecisionAuditWriter : IChallengeDecisionAuditWriter
{
    public List<string> Events { get; } = [];

    public Task WriteApprovedAsync(
        Challenge challenge,
        RegisteredDevice device,
        bool biometricVerified,
        CancellationToken cancellationToken)
    {
        Events.Add($"approved:{challenge.Id}:{device.Id}:{biometricVerified}");
        return Task.CompletedTask;
    }

    public Task WriteDeniedAsync(
        Challenge challenge,
        RegisteredDevice device,
        bool hasReason,
        CancellationToken cancellationToken)
    {
        Events.Add($"denied:{challenge.Id}:{device.Id}:{hasReason}");
        return Task.CompletedTask;
    }
}
