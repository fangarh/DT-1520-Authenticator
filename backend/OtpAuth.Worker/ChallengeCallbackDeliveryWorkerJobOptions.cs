namespace OtpAuth.Worker;

public sealed class ChallengeCallbackDeliveryWorkerJobOptions
{
    public bool Enabled { get; init; } = true;

    public int IntervalSeconds { get; init; } = 15;

    public int LeaseSeconds { get; init; } = 60;

    public int RetryDelaySeconds { get; init; } = 30;

    public int BatchSize { get; init; } = 20;

    public int MaxAttempts { get; init; } = 5;

    public TimeSpan GetInterval()
    {
        if (IntervalSeconds <= 0)
        {
            throw new InvalidOperationException("WorkerJobs:ChallengeCallbackDelivery:IntervalSeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(IntervalSeconds);
    }

    public TimeSpan GetLeaseDuration()
    {
        if (LeaseSeconds <= 0)
        {
            throw new InvalidOperationException("WorkerJobs:ChallengeCallbackDelivery:LeaseSeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(LeaseSeconds);
    }

    public TimeSpan GetRetryDelay()
    {
        if (RetryDelaySeconds <= 0)
        {
            throw new InvalidOperationException("WorkerJobs:ChallengeCallbackDelivery:RetryDelaySeconds must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(RetryDelaySeconds);
    }

    public int GetBatchSize()
    {
        if (BatchSize <= 0)
        {
            throw new InvalidOperationException("WorkerJobs:ChallengeCallbackDelivery:BatchSize must be greater than zero.");
        }

        return BatchSize;
    }

    public int GetMaxAttempts()
    {
        if (MaxAttempts <= 0)
        {
            throw new InvalidOperationException("WorkerJobs:ChallengeCallbackDelivery:MaxAttempts must be greater than zero.");
        }

        return MaxAttempts;
    }
}
