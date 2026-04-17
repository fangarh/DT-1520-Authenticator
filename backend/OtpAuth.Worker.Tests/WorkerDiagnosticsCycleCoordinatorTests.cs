using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace OtpAuth.Worker.Tests;

public sealed class WorkerDiagnosticsCycleCoordinatorTests
{
    [Fact]
    public async Task RunCycleAsync_PublishesHealthySnapshotAndResetsFailureCount()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 04, 15, 12, 00, 00, TimeSpan.Zero));
        var publisher = new InMemoryWorkerHeartbeatPublisher();
        var coordinator = new WorkerDiagnosticsCycleCoordinator(
            [
                new StubWorkerDependencyProbe("postgres", WorkerDependencyProbeResult.Healthy("postgres")),
                new StubWorkerDependencyProbe("redis", WorkerDependencyProbeResult.Healthy("redis"))
            ],
            [],
            publisher,
            timeProvider,
            NullLogger<WorkerDiagnosticsCycleCoordinator>.Instance);

        var state = await coordinator.RunCycleAsync(
            new WorkerDiagnosticsState
            {
                StartedAtUtc = new DateTimeOffset(2026, 04, 15, 11, 59, 00, TimeSpan.Zero),
                ConsecutiveFailureCount = 2,
                JobStates = []
            },
            CancellationToken.None);

        Assert.Equal(0, state.ConsecutiveFailureCount);
        Assert.NotNull(publisher.LastSnapshot);
        Assert.Equal("healthy", publisher.LastSnapshot!.ExecutionOutcome);
        Assert.All(publisher.LastSnapshot.DependencyStatuses, status => Assert.Equal("healthy", status.Status));
        Assert.Empty(publisher.LastSnapshot.JobStatuses);
    }

    [Fact]
    public async Task RunCycleAsync_PublishesDegradedSnapshotAndIncrementsFailureCount()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 04, 15, 12, 05, 00, TimeSpan.Zero));
        var publisher = new InMemoryWorkerHeartbeatPublisher();
        var coordinator = new WorkerDiagnosticsCycleCoordinator(
            [
                new StubWorkerDependencyProbe("postgres", WorkerDependencyProbeResult.Unhealthy("postgres", "connection_failed")),
                new StubWorkerDependencyProbe("redis", WorkerDependencyProbeResult.Healthy("redis"))
            ],
            [],
            publisher,
            timeProvider,
            NullLogger<WorkerDiagnosticsCycleCoordinator>.Instance);

        var state = await coordinator.RunCycleAsync(
            new WorkerDiagnosticsState
            {
                StartedAtUtc = new DateTimeOffset(2026, 04, 15, 12, 00, 00, TimeSpan.Zero),
                ConsecutiveFailureCount = 1,
                JobStates = []
            },
            CancellationToken.None);

        Assert.Equal(2, state.ConsecutiveFailureCount);
        Assert.NotNull(publisher.LastSnapshot);
        Assert.Equal("degraded", publisher.LastSnapshot!.ExecutionOutcome);
        Assert.Contains(
            publisher.LastSnapshot.DependencyStatuses,
            status => status.Name == "postgres" && status.FailureKind == "connection_failed");
    }

    [Fact]
    public async Task RunCycleAsync_ExecutesDueJobAndPublishesJobProgress()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 04, 15, 12, 10, 00, TimeSpan.Zero));
        var publisher = new InMemoryWorkerHeartbeatPublisher();
        var job = new StubWorkerJob(
            "security_data_cleanup",
            TimeSpan.FromMinutes(5),
            WorkerJobRunResult.Create(
                "cleanup_completed",
                new WorkerJobMetricSnapshot("deletedTotal", 7)));
        var coordinator = new WorkerDiagnosticsCycleCoordinator(
            [
                new StubWorkerDependencyProbe("postgres", WorkerDependencyProbeResult.Healthy("postgres")),
                new StubWorkerDependencyProbe("redis", WorkerDependencyProbeResult.Healthy("redis"))
            ],
            [job],
            publisher,
            timeProvider,
            NullLogger<WorkerDiagnosticsCycleCoordinator>.Instance);

        var state = await coordinator.RunCycleAsync(
            new WorkerDiagnosticsState
            {
                StartedAtUtc = new DateTimeOffset(2026, 04, 15, 12, 00, 00, TimeSpan.Zero),
                ConsecutiveFailureCount = 0,
                JobStates = []
            },
            CancellationToken.None);

        Assert.Equal(1, job.ExecuteCallCount);
        Assert.Single(state.JobStates);
        Assert.NotNull(publisher.LastSnapshot);
        Assert.Single(publisher.LastSnapshot!.JobStatuses);

        var snapshot = publisher.LastSnapshot.JobStatuses[0];
        Assert.Equal("security_data_cleanup", snapshot.Name);
        Assert.Equal("healthy", snapshot.Status);
        Assert.Equal(300, snapshot.IntervalSeconds);
        Assert.True(snapshot.IsDue);
        Assert.Equal(1, snapshot.SuccessfulRunCount);
        Assert.Equal("cleanup_completed", snapshot.LastSummary);
        Assert.Collection(
            snapshot.LastMetrics,
            metric =>
            {
                Assert.Equal("deletedTotal", metric.Name);
                Assert.Equal(7, metric.Value);
            });
    }

    [Fact]
    public async Task RunCycleAsync_BlocksJobsWhenDependenciesAreUnavailable()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 04, 15, 12, 15, 00, TimeSpan.Zero));
        var publisher = new InMemoryWorkerHeartbeatPublisher();
        var job = new StubWorkerJob(
            "security_data_cleanup",
            TimeSpan.FromMinutes(5),
            WorkerJobRunResult.Create("cleanup_completed"));
        var coordinator = new WorkerDiagnosticsCycleCoordinator(
            [
                new StubWorkerDependencyProbe("postgres", WorkerDependencyProbeResult.Unhealthy("postgres", "connection_failed"))
            ],
            [job],
            publisher,
            timeProvider,
            NullLogger<WorkerDiagnosticsCycleCoordinator>.Instance);

        await coordinator.RunCycleAsync(
            new WorkerDiagnosticsState
            {
                StartedAtUtc = new DateTimeOffset(2026, 04, 15, 12, 00, 00, TimeSpan.Zero),
                ConsecutiveFailureCount = 0,
                JobStates = []
            },
            CancellationToken.None);

        Assert.Equal(0, job.ExecuteCallCount);
        Assert.NotNull(publisher.LastSnapshot);
        var snapshot = Assert.Single(publisher.LastSnapshot!.JobStatuses);
        Assert.Equal("blocked", snapshot.Status);
        Assert.Equal("blocked_by_dependency", snapshot.FailureKind);
    }

    [Fact]
    public async Task RunCycleAsync_MarksWorkerDegradedWhenJobFails()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 04, 15, 12, 20, 00, TimeSpan.Zero));
        var publisher = new InMemoryWorkerHeartbeatPublisher();
        var job = new StubWorkerJob(
            "security_data_cleanup",
            TimeSpan.FromMinutes(5),
            error: new InvalidOperationException("boom"));
        var coordinator = new WorkerDiagnosticsCycleCoordinator(
            [
                new StubWorkerDependencyProbe("postgres", WorkerDependencyProbeResult.Healthy("postgres")),
                new StubWorkerDependencyProbe("redis", WorkerDependencyProbeResult.Healthy("redis"))
            ],
            [job],
            publisher,
            timeProvider,
            NullLogger<WorkerDiagnosticsCycleCoordinator>.Instance);

        var state = await coordinator.RunCycleAsync(
            new WorkerDiagnosticsState
            {
                StartedAtUtc = new DateTimeOffset(2026, 04, 15, 12, 00, 00, TimeSpan.Zero),
                ConsecutiveFailureCount = 0,
                JobStates = []
            },
            CancellationToken.None);

        Assert.Equal(1, state.ConsecutiveFailureCount);
        Assert.NotNull(publisher.LastSnapshot);
        Assert.Equal("degraded", publisher.LastSnapshot!.ExecutionOutcome);
        var snapshot = Assert.Single(publisher.LastSnapshot.JobStatuses);
        Assert.Equal("degraded", snapshot.Status);
        Assert.Equal("execution_failed", snapshot.FailureKind);
        Assert.Equal(1, snapshot.FailedRunCount);
    }

    private sealed class StubWorkerDependencyProbe(string name, WorkerDependencyProbeResult result) : IWorkerDependencyProbe
    {
        public string Name => name;

        public Task<WorkerDependencyProbeResult> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class StubWorkerJob(
        string name,
        TimeSpan interval,
        WorkerJobRunResult? result = null,
        Exception? error = null) : IWorkerJob
    {
        public int ExecuteCallCount { get; private set; }

        public string Name => name;

        public bool IsEnabled => true;

        public TimeSpan GetInterval() => interval;

        public Task<WorkerJobRunResult> ExecuteAsync(DateTimeOffset utcNow, CancellationToken cancellationToken)
        {
            ExecuteCallCount++;

            if (error is not null)
            {
                throw error;
            }

            return Task.FromResult(result ?? WorkerJobRunResult.Create("completed"));
        }
    }

    private sealed class InMemoryWorkerHeartbeatPublisher : IWorkerHeartbeatPublisher
    {
        public WorkerHeartbeatSnapshot? LastSnapshot { get; private set; }

        public Task PublishAsync(WorkerHeartbeatSnapshot snapshot, CancellationToken cancellationToken)
        {
            LastSnapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
