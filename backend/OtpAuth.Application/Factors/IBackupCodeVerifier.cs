using OtpAuth.Domain.Challenges;

namespace OtpAuth.Application.Factors;

public interface IBackupCodeVerifier
{
    Task<BackupCodeVerificationResult> VerifyAsync(
        Challenge challenge,
        string code,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}
