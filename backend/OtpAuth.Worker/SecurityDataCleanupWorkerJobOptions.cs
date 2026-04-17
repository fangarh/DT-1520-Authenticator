namespace OtpAuth.Worker;

public sealed class SecurityDataCleanupWorkerJobOptions
{
    public bool Enabled { get; init; } = true;

    public int IntervalSeconds { get; init; } = 300;

    public TimeSpan GetInterval()
    {
        if (IntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "WorkerJobs:SecurityDataCleanup:IntervalSeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(IntervalSeconds);
    }
}
