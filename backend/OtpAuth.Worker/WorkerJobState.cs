namespace OtpAuth.Worker;

public sealed record WorkerJobState(
    string Name,
    string LastOutcome,
    DateTimeOffset? LastStartedAtUtc,
    DateTimeOffset? LastCompletedAtUtc,
    DateTimeOffset? LastSuccessfulCompletedAtUtc,
    int SuccessfulRunCount,
    int FailedRunCount,
    int ConsecutiveFailureCount,
    string? LastSummary,
    string? LastFailureKind,
    IReadOnlyList<WorkerJobMetricSnapshot> LastMetrics)
{
    public static WorkerJobState CreateInitial(string name)
    {
        return new WorkerJobState(
            Name: name,
            LastOutcome: "never_run",
            LastStartedAtUtc: null,
            LastCompletedAtUtc: null,
            LastSuccessfulCompletedAtUtc: null,
            SuccessfulRunCount: 0,
            FailedRunCount: 0,
            ConsecutiveFailureCount: 0,
            LastSummary: null,
            LastFailureKind: null,
            LastMetrics: []);
    }

    public bool IsDue(DateTimeOffset utcNow, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Worker job interval must be greater than zero.");
        }

        return LastCompletedAtUtc is null || utcNow - LastCompletedAtUtc.Value >= interval;
    }

    public WorkerJobState RecordSuccess(
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        WorkerJobRunResult result)
    {
        return this with
        {
            LastOutcome = "healthy",
            LastStartedAtUtc = startedAtUtc,
            LastCompletedAtUtc = completedAtUtc,
            LastSuccessfulCompletedAtUtc = completedAtUtc,
            SuccessfulRunCount = SuccessfulRunCount + 1,
            ConsecutiveFailureCount = 0,
            LastSummary = result.Summary,
            LastFailureKind = null,
            LastMetrics = result.Metrics.ToArray()
        };
    }

    public WorkerJobState RecordFailure(
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        string failureKind)
    {
        return this with
        {
            LastOutcome = "degraded",
            LastStartedAtUtc = startedAtUtc,
            LastCompletedAtUtc = completedAtUtc,
            FailedRunCount = FailedRunCount + 1,
            ConsecutiveFailureCount = ConsecutiveFailureCount + 1,
            LastSummary = "execution_failed",
            LastFailureKind = failureKind
        };
    }

    public WorkerJobStatusSnapshot ToSnapshot(
        string status,
        int intervalSeconds,
        bool isDue,
        string? failureKind)
    {
        return new WorkerJobStatusSnapshot(
            Name,
            status,
            intervalSeconds,
            isDue,
            LastStartedAtUtc,
            LastCompletedAtUtc,
            LastSuccessfulCompletedAtUtc,
            SuccessfulRunCount,
            FailedRunCount,
            ConsecutiveFailureCount,
            LastSummary,
            failureKind,
            LastMetrics.ToArray());
    }
}
