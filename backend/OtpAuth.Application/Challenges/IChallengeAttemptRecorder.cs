namespace OtpAuth.Application.Challenges;

public interface IChallengeAttemptRecorder
{
    Task RecordAsync(ChallengeAttemptRecord attempt, CancellationToken cancellationToken);
}
