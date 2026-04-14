namespace OtpAuth.Infrastructure.Factors;

public sealed class TotpSecretsReEncryptionService
{
    private const int DefaultBatchSize = 100;

    private readonly ITotpEnrollmentMaintenanceStore _maintenanceStore;
    private readonly TotpSecretProtector _secretProtector;

    public TotpSecretsReEncryptionService(
        ITotpEnrollmentMaintenanceStore maintenanceStore,
        TotpSecretProtector secretProtector)
    {
        _maintenanceStore = maintenanceStore;
        _secretProtector = secretProtector;
    }

    public async Task<TotpSecretsReEncryptionResult> ReEncryptAsync(
        int? batchSize,
        CancellationToken cancellationToken)
    {
        var resolvedBatchSize = batchSize.GetValueOrDefault(DefaultBatchSize);
        if (resolvedBatchSize <= 0)
        {
            throw new InvalidOperationException("Batch size must be greater than zero.");
        }

        var scannedRecords = 0;
        var reEncryptedRecords = 0;
        var skippedRecords = 0;

        while (true)
        {
            var records = await _maintenanceStore.GetRecordsRequiringReEncryptionAsync(
                _secretProtector.CurrentKeyVersion,
                resolvedBatchSize,
                cancellationToken);
            if (records.Count == 0)
            {
                break;
            }

            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scannedRecords++;

                var plaintext = _secretProtector.Unprotect(new TotpProtectedSecret
                {
                    Ciphertext = record.Ciphertext,
                    Nonce = record.Nonce,
                    Tag = record.Tag,
                    KeyVersion = record.KeyVersion,
                });
                var reProtectedSecret = _secretProtector.Protect(plaintext);
                var updated = await _maintenanceStore.UpdateProtectedSecretAsync(
                    record.EnrollmentId,
                    record.KeyVersion,
                    reProtectedSecret,
                    cancellationToken);

                if (updated)
                {
                    reEncryptedRecords++;
                }
                else
                {
                    skippedRecords++;
                }
            }

            if (records.Count < resolvedBatchSize)
            {
                break;
            }
        }

        return new TotpSecretsReEncryptionResult
        {
            ScannedRecords = scannedRecords,
            ReEncryptedRecords = reEncryptedRecords,
            SkippedRecords = skippedRecords,
        };
    }
}
