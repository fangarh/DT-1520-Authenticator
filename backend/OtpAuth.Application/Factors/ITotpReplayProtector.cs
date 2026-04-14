namespace OtpAuth.Application.Factors;

public interface ITotpReplayProtector
{
    Task<bool> TryReserveAsync(
        Guid enrollmentId,
        long timeStep,
        DateTimeOffset usedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}
