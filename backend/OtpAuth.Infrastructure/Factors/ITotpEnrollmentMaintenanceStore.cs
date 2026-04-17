namespace OtpAuth.Infrastructure.Factors;

public interface ITotpEnrollmentMaintenanceStore
{
    Task<IReadOnlyCollection<TotpEnrollmentProtectedRecord>> GetRecordsRequiringReEncryptionAsync(
        int currentKeyVersion,
        int batchSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TotpEnrollmentKeyVersionUsage>> GetKeyVersionUsageAsync(
        CancellationToken cancellationToken);

    Task<bool> UpdateProtectedSecretAsync(
        Guid enrollmentId,
        int expectedKeyVersion,
        TotpProtectedSecret protectedSecret,
        CancellationToken cancellationToken);
}
