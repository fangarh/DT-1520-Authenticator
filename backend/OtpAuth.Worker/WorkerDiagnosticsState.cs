namespace OtpAuth.Worker;

public sealed record WorkerDiagnosticsState
{
    public required DateTimeOffset StartedAtUtc { get; init; }

    public required int ConsecutiveFailureCount { get; init; }

    public IReadOnlyList<WorkerJobState> JobStates { get; init; } = [];
}
