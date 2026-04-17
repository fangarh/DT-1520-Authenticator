using OtpAuth.Infrastructure.Persistence;

namespace OtpAuth.Worker;

public interface ISecurityDataCleanupRunner
{
    Task<SecurityDataCleanupResult> CleanupAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken);
}
