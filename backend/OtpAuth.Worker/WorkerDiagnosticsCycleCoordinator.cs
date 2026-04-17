using Microsoft.Extensions.Logging;

namespace OtpAuth.Worker;

public sealed class WorkerDiagnosticsCycleCoordinator(
    IEnumerable<IWorkerDependencyProbe> dependencyProbes,
    IEnumerable<IWorkerJob> workerJobs,
    IWorkerHeartbeatPublisher heartbeatPublisher,
    TimeProvider timeProvider,
    ILogger<WorkerDiagnosticsCycleCoordinator> logger)
{
    private readonly IReadOnlyList<IWorkerDependencyProbe> _dependencyProbes = dependencyProbes.ToList();
    private readonly IReadOnlyList<IWorkerJob> _workerJobs = workerJobs.ToList();
    private readonly IWorkerHeartbeatPublisher _heartbeatPublisher = heartbeatPublisher;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<WorkerDiagnosticsCycleCoordinator> _logger = logger;

    public async Task<WorkerDiagnosticsState> RunCycleAsync(
        WorkerDiagnosticsState previousState,
        CancellationToken cancellationToken)
    {
        var executionStartedAtUtc = _timeProvider.GetUtcNow();
        var probeResults = new List<WorkerDependencyProbeResult>(_dependencyProbes.Count);

        foreach (var dependencyProbe in _dependencyProbes)
        {
            probeResults.Add(await dependencyProbe.CheckAsync(cancellationToken));
        }

        var dependenciesHealthy = probeResults.All(result => result.IsHealthy);
        var previousJobStates = previousState.JobStates.ToDictionary(state => state.Name, StringComparer.Ordinal);
        var currentJobStates = new List<WorkerJobState>(_workerJobs.Count);
        var jobSnapshots = new List<WorkerJobStatusSnapshot>(_workerJobs.Count);
        var hasJobFailures = false;

        foreach (var workerJob in _workerJobs)
        {
            var previousJobState = previousJobStates.TryGetValue(workerJob.Name, out var knownState)
                ? knownState
                : WorkerJobState.CreateInitial(workerJob.Name);

            var currentJobState = previousJobState;
            var failureKind = currentJobState.LastFailureKind;
            var intervalSeconds = workerJob.IsEnabled
                ? (int)workerJob.GetInterval().TotalSeconds
                : 0;
            var isDue = workerJob.IsEnabled && currentJobState.IsDue(executionStartedAtUtc, workerJob.GetInterval());
            var snapshotStatus = ResolveIdleStatus(currentJobState);

            if (!workerJob.IsEnabled)
            {
                snapshotStatus = "disabled";
                isDue = false;
                failureKind = null;
            }
            else if (!dependenciesHealthy)
            {
                snapshotStatus = "blocked";
                failureKind = "blocked_by_dependency";
            }
            else if (isDue)
            {
                var jobStartedAtUtc = _timeProvider.GetUtcNow();

                try
                {
                    var result = await workerJob.ExecuteAsync(jobStartedAtUtc, cancellationToken);
                    var jobCompletedAtUtc = _timeProvider.GetUtcNow();

                    currentJobState = previousJobState.RecordSuccess(jobStartedAtUtc, jobCompletedAtUtc, result);
                    snapshotStatus = "healthy";
                    failureKind = null;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    var jobCompletedAtUtc = _timeProvider.GetUtcNow();

                    currentJobState = previousJobState.RecordFailure(
                        jobStartedAtUtc,
                        jobCompletedAtUtc,
                        "execution_failed");
                    snapshotStatus = "degraded";
                    failureKind = currentJobState.LastFailureKind;
                    hasJobFailures = true;

                    _logger.LogWarning(
                        "Worker job '{JobName}' failed with {ExceptionType}.",
                        workerJob.Name,
                        exception.GetType().Name);
                }
            }

            currentJobStates.Add(currentJobState);
            jobSnapshots.Add(currentJobState.ToSnapshot(snapshotStatus, intervalSeconds, isDue, failureKind));
        }

        var executionCompletedAtUtc = _timeProvider.GetUtcNow();
        var hasProbeFailures = probeResults.Any(result => !result.IsHealthy);
        var hasFailures = hasProbeFailures || hasJobFailures;
        var consecutiveFailureCount = hasFailures
            ? previousState.ConsecutiveFailureCount + 1
            : 0;

        var snapshot = new WorkerHeartbeatSnapshot(
            ServiceName: "OtpAuth.Worker",
            StartedAtUtc: previousState.StartedAtUtc,
            LastHeartbeatUtc: executionCompletedAtUtc,
            LastExecutionStartedUtc: executionStartedAtUtc,
            LastExecutionCompletedUtc: executionCompletedAtUtc,
            ExecutionOutcome: hasFailures ? "degraded" : "healthy",
            ConsecutiveFailureCount: consecutiveFailureCount,
            ProcessId: Environment.ProcessId,
            DependencyStatuses: probeResults
                .Select(result => new WorkerDependencyStatusSnapshot(
                    Name: result.Name,
                    Status: result.IsHealthy ? "healthy" : "degraded",
                    CheckedAtUtc: executionCompletedAtUtc,
                    FailureKind: result.FailureKind))
                .ToArray(),
            JobStatuses: jobSnapshots.ToArray());

        await _heartbeatPublisher.PublishAsync(snapshot, cancellationToken);

        return previousState with
        {
            ConsecutiveFailureCount = consecutiveFailureCount,
            JobStates = currentJobStates.ToArray()
        };
    }

    private static string ResolveIdleStatus(WorkerJobState jobState)
    {
        return jobState.LastOutcome switch
        {
            "degraded" => "degraded",
            "healthy" => "idle",
            _ => "idle"
        };
    }
}
