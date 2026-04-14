using OtpAuth.Infrastructure.Factors;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class TotpSecretsReEncryptionServiceTests
{
    private static readonly byte[] LegacyKey = "ABCDEFGHIJKLMNOPQRSTUVWX12345678"u8.ToArray();
    private static readonly byte[] CurrentKey = "0123456789ABCDEF0123456789ABCDEF"u8.ToArray();

    [Fact]
    public async Task ReEncryptAsync_ReEncryptsLegacyRecordsWithCurrentKeyVersion()
    {
        var legacyProtector = new TotpSecretProtector(new TotpProtectionOptions
        {
            CurrentKey = Convert.ToBase64String(LegacyKey),
            CurrentKeyVersion = 1,
        });
        var currentProtector = new TotpSecretProtector(new TotpProtectionOptions
        {
            CurrentKey = Convert.ToBase64String(CurrentKey),
            CurrentKeyVersion = 2,
            AdditionalKeys =
            [
                new TotpProtectionKeyOptions
                {
                    KeyVersion = 1,
                    Key = Convert.ToBase64String(LegacyKey),
                },
            ],
        });
        var protectedSecret = legacyProtector.Protect("ZYXWVUTSRQPONMLKJIHGFEDCBA123456"u8.ToArray());
        var store = new InMemoryTotpEnrollmentMaintenanceStore(
            [
                new TotpEnrollmentProtectedRecord
                {
                    EnrollmentId = Guid.NewGuid(),
                    KeyVersion = protectedSecret.KeyVersion,
                    Ciphertext = protectedSecret.Ciphertext,
                    Nonce = protectedSecret.Nonce,
                    Tag = protectedSecret.Tag,
                },
            ]);
        var service = new TotpSecretsReEncryptionService(store, currentProtector);

        var result = await service.ReEncryptAsync(batchSize: 100, CancellationToken.None);

        Assert.Equal(1, result.ScannedRecords);
        Assert.Equal(1, result.ReEncryptedRecords);
        var updated = Assert.Single(store.UpdatedRecords);
        Assert.Equal(2, updated.ProtectedSecret.KeyVersion);

        var currentOnlyProtector = new TotpSecretProtector(new TotpProtectionOptions
        {
            CurrentKey = Convert.ToBase64String(CurrentKey),
            CurrentKeyVersion = 2,
        });
        var plaintext = currentOnlyProtector.Unprotect(updated.ProtectedSecret);
        Assert.Equal("ZYXWVUTSRQPONMLKJIHGFEDCBA123456"u8.ToArray(), plaintext);
    }

    [Fact]
    public async Task ReEncryptAsync_Throws_WhenBatchSizeIsInvalid()
    {
        var service = new TotpSecretsReEncryptionService(
            new InMemoryTotpEnrollmentMaintenanceStore([]),
            new TotpSecretProtector(new TotpProtectionOptions
            {
                CurrentKey = Convert.ToBase64String(CurrentKey),
                CurrentKeyVersion = 2,
            }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReEncryptAsync(batchSize: 0, CancellationToken.None));

        Assert.Contains("Batch size", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InMemoryTotpEnrollmentMaintenanceStore : ITotpEnrollmentMaintenanceStore
    {
        private readonly List<TotpEnrollmentProtectedRecord> _records;

        public InMemoryTotpEnrollmentMaintenanceStore(IReadOnlyCollection<TotpEnrollmentProtectedRecord> records)
        {
            _records = records.ToList();
        }

        public List<(Guid EnrollmentId, int ExpectedKeyVersion, TotpProtectedSecret ProtectedSecret)> UpdatedRecords { get; } = [];

        public Task<IReadOnlyCollection<TotpEnrollmentProtectedRecord>> GetRecordsRequiringReEncryptionAsync(
            int currentKeyVersion,
            int batchSize,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<TotpEnrollmentProtectedRecord>>(
                _records
                    .Where(record => record.KeyVersion != currentKeyVersion)
                    .Take(batchSize)
                    .ToArray());
        }

        public Task<bool> UpdateProtectedSecretAsync(
            Guid enrollmentId,
            int expectedKeyVersion,
            TotpProtectedSecret protectedSecret,
            CancellationToken cancellationToken)
        {
            var record = _records.Single(item => item.EnrollmentId == enrollmentId);
            if (record.KeyVersion != expectedKeyVersion)
            {
                return Task.FromResult(false);
            }

            _records.Remove(record);
            _records.Add(new TotpEnrollmentProtectedRecord
            {
                EnrollmentId = enrollmentId,
                KeyVersion = protectedSecret.KeyVersion,
                Ciphertext = protectedSecret.Ciphertext,
                Nonce = protectedSecret.Nonce,
                Tag = protectedSecret.Tag,
            });
            UpdatedRecords.Add((enrollmentId, expectedKeyVersion, protectedSecret));
            return Task.FromResult(true);
        }
    }
}
