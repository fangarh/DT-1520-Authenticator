namespace OtpAuth.Application.Factors;

public interface IBackupCodeStore
{
    Task<IReadOnlyCollection<BackupCodeCredential>> ListActiveAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken);

    Task<bool> TryMarkUsedAsync(
        Guid backupCodeId,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken);
}
