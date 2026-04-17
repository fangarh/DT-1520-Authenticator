namespace OtpAuth.Worker;

public sealed class PushChallengeDeliveryWorkerJobOptions
{
    public bool Enabled { get; init; } = true;

    public int IntervalSeconds { get; init; } = 15;

    public int BatchSize { get; init; } = 20;

    public int LeaseSeconds { get; init; } = 60;

    public int RetryDelaySeconds { get; init; } = 30;

    public int MaxAttempts { get; init; } = 5;

    public TimeSpan GetInterval()
    {
        if (IntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "WorkerJobs:PushChallengeDelivery:IntervalSeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(IntervalSeconds);
    }

    public TimeSpan GetLeaseDuration()
    {
        if (LeaseSeconds <= 0)
        {
            throw new InvalidOperationException(
                "WorkerJobs:PushChallengeDelivery:LeaseSeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(LeaseSeconds);
    }

    public TimeSpan GetRetryDelay()
    {
        if (RetryDelaySeconds <= 0)
        {
            throw new InvalidOperationException(
                "WorkerJobs:PushChallengeDelivery:RetryDelaySeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(RetryDelaySeconds);
    }

    public int GetBatchSize()
    {
        if (BatchSize <= 0)
        {
            throw new InvalidOperationException(
                "WorkerJobs:PushChallengeDelivery:BatchSize must be greater than zero.");
        }

        return BatchSize;
    }

    public int GetMaxAttempts()
    {
        if (MaxAttempts <= 0)
        {
            throw new InvalidOperationException(
                "WorkerJobs:PushChallengeDelivery:MaxAttempts must be greater than zero.");
        }

        return MaxAttempts;
    }
}
