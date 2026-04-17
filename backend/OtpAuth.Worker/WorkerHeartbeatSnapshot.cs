namespace OtpAuth.Worker;

public sealed record WorkerHeartbeatSnapshot(
    string ServiceName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastHeartbeatUtc,
    DateTimeOffset LastExecutionStartedUtc,
    DateTimeOffset LastExecutionCompletedUtc,
    string ExecutionOutcome,
    int ConsecutiveFailureCount,
    int ProcessId,
    IReadOnlyList<WorkerDependencyStatusSnapshot> DependencyStatuses,
    IReadOnlyList<WorkerJobStatusSnapshot> JobStatuses);

public sealed record WorkerDependencyStatusSnapshot(
    string Name,
    string Status,
    DateTimeOffset CheckedAtUtc,
    string? FailureKind);

public sealed record WorkerJobStatusSnapshot(
    string Name,
    string Status,
    int IntervalSeconds,
    bool IsDue,
    DateTimeOffset? LastStartedAtUtc,
    DateTimeOffset? LastCompletedAtUtc,
    DateTimeOffset? LastSuccessfulCompletedAtUtc,
    int SuccessfulRunCount,
    int FailedRunCount,
    int ConsecutiveFailureCount,
    string? LastSummary,
    string? FailureKind,
    IReadOnlyList<WorkerJobMetricSnapshot> LastMetrics);

public sealed record WorkerJobMetricSnapshot(
    string Name,
    long Value);
