using OtpAuth.Application.Factors;
using OtpAuth.Domain.Challenges;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresBackupCodeVerifier : IBackupCodeVerifier
{
    private readonly IBackupCodeStore _backupCodeStore;
    private readonly Pbkdf2BackupCodeHasher _backupCodeHasher;

    public PostgresBackupCodeVerifier(
        IBackupCodeStore backupCodeStore,
        Pbkdf2BackupCodeHasher backupCodeHasher)
    {
        _backupCodeStore = backupCodeStore;
        _backupCodeHasher = backupCodeHasher;
    }

    public async Task<BackupCodeVerificationResult> VerifyAsync(
        Challenge challenge,
        string code,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!BackupCodeFormat.TryNormalize(code, out var normalizedCode, out _))
        {
            return BackupCodeVerificationResult.InvalidCode();
        }

        var activeCodes = await _backupCodeStore.ListActiveAsync(
            challenge.TenantId,
            challenge.ApplicationClientId,
            challenge.ExternalUserId,
            cancellationToken);

        foreach (var backupCode in activeCodes)
        {
            if (!_backupCodeHasher.Verify(normalizedCode, backupCode.CodeHash))
            {
                continue;
            }

            var markedUsed = await _backupCodeStore.TryMarkUsedAsync(
                backupCode.BackupCodeId,
                timestamp,
                cancellationToken);
            return markedUsed
                ? BackupCodeVerificationResult.Valid(backupCode.BackupCodeId)
                : BackupCodeVerificationResult.InvalidCode();
        }

        return BackupCodeVerificationResult.InvalidCode();
    }
}
