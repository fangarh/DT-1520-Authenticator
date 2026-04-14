namespace OtpAuth.Application.Factors;

public interface ITotpEnrollmentStore
{
    Task<TotpEnrollmentSecret?> GetActiveAsync(
        Guid tenantId,
        Guid applicationClientId,
        string externalUserId,
        CancellationToken cancellationToken);

    Task MarkUsedAsync(Guid enrollmentId, DateTimeOffset usedAt, CancellationToken cancellationToken);
}
