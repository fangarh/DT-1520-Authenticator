using System.Text.Json;
using Microsoft.Extensions.Options;
using Xunit;

namespace OtpAuth.Worker.Tests;

public sealed class FileWorkerHeartbeatPublisherTests : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _tempRoot;

    public FileWorkerHeartbeatPublisherTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"otpauth-worker-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task PublishAsync_CreatesHeartbeatFileWithExpectedPayload()
    {
        var heartbeatFilePath = Path.Combine(_tempRoot, "health", "heartbeat.json");
        var publisher = CreatePublisher(heartbeatFilePath);

        var snapshot = new WorkerHeartbeatSnapshot(
            ServiceName: "OtpAuth.Worker",
            StartedAtUtc: new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero),
            LastHeartbeatUtc: new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero),
            LastExecutionStartedUtc: new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero),
            LastExecutionCompletedUtc: new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero),
            ExecutionOutcome: "healthy",
            ConsecutiveFailureCount: 0,
            ProcessId: 4242,
            DependencyStatuses:
            [
                new WorkerDependencyStatusSnapshot("postgres", "healthy", new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero), null),
                new WorkerDependencyStatusSnapshot("redis", "healthy", new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero), null)
            ],
            JobStatuses:
            [
                new WorkerJobStatusSnapshot(
                    "security_data_cleanup",
                    "healthy",
                    300,
                    true,
                    new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero),
                    new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero),
                    new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero),
                    1,
                    0,
                    0,
                    "cleanup_completed",
                    null,
                    [new WorkerJobMetricSnapshot("deletedTotal", 4)])
            ]);

        await publisher.PublishAsync(snapshot, CancellationToken.None);

        Assert.True(File.Exists(heartbeatFilePath));

        var persistedSnapshot = JsonSerializer.Deserialize<WorkerHeartbeatSnapshot>(
            await File.ReadAllTextAsync(heartbeatFilePath),
            SerializerOptions);

        Assert.NotNull(persistedSnapshot);
        Assert.Equal(snapshot.ServiceName, persistedSnapshot!.ServiceName);
        Assert.Equal(snapshot.StartedAtUtc, persistedSnapshot.StartedAtUtc);
        Assert.Equal(snapshot.LastHeartbeatUtc, persistedSnapshot.LastHeartbeatUtc);
        Assert.Equal(snapshot.LastExecutionStartedUtc, persistedSnapshot.LastExecutionStartedUtc);
        Assert.Equal(snapshot.LastExecutionCompletedUtc, persistedSnapshot.LastExecutionCompletedUtc);
        Assert.Equal(snapshot.ExecutionOutcome, persistedSnapshot.ExecutionOutcome);
        Assert.Equal(snapshot.ConsecutiveFailureCount, persistedSnapshot.ConsecutiveFailureCount);
        Assert.Equal(snapshot.ProcessId, persistedSnapshot.ProcessId);
        Assert.Equal(snapshot.DependencyStatuses, persistedSnapshot.DependencyStatuses);
        var jobSnapshot = Assert.Single(persistedSnapshot.JobStatuses);
        Assert.Equal("security_data_cleanup", jobSnapshot.Name);
        Assert.Equal("healthy", jobSnapshot.Status);
        Assert.Equal(300, jobSnapshot.IntervalSeconds);
        Assert.True(jobSnapshot.IsDue);
        Assert.Equal("cleanup_completed", jobSnapshot.LastSummary);
        Assert.Collection(
            jobSnapshot.LastMetrics,
            metric =>
            {
                Assert.Equal("deletedTotal", metric.Name);
                Assert.Equal(4, metric.Value);
            });
    }

    [Fact]
    public async Task PublishAsync_ReplacesExistingHeartbeatContent()
    {
        var heartbeatFilePath = Path.Combine(_tempRoot, "heartbeat.json");
        var publisher = CreatePublisher(heartbeatFilePath);

        var firstSnapshot = new WorkerHeartbeatSnapshot(
            ServiceName: "OtpAuth.Worker",
            StartedAtUtc: new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero),
            LastHeartbeatUtc: new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero),
            LastExecutionStartedUtc: new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero),
            LastExecutionCompletedUtc: new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero),
            ExecutionOutcome: "healthy",
            ConsecutiveFailureCount: 0,
            ProcessId: 1000,
            DependencyStatuses:
            [
                new WorkerDependencyStatusSnapshot("postgres", "healthy", new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero), null)
            ],
            JobStatuses:
            [
                new WorkerJobStatusSnapshot(
                    "security_data_cleanup",
                    "healthy",
                    300,
                    true,
                    new DateTimeOffset(2026, 04, 15, 10, 00, 00, TimeSpan.Zero),
                    new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero),
                    new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero),
                    1,
                    0,
                    0,
                    "cleanup_completed",
                    null,
                    [new WorkerJobMetricSnapshot("deletedTotal", 4)])
            ]);

        var secondSnapshot = firstSnapshot with
        {
            LastHeartbeatUtc = new DateTimeOffset(2026, 04, 15, 10, 01, 00, TimeSpan.Zero),
            LastExecutionStartedUtc = new DateTimeOffset(2026, 04, 15, 10, 00, 45, TimeSpan.Zero),
            LastExecutionCompletedUtc = new DateTimeOffset(2026, 04, 15, 10, 01, 00, TimeSpan.Zero),
            ExecutionOutcome = "degraded",
            ConsecutiveFailureCount = 1,
            ProcessId = 1001,
            DependencyStatuses =
            [
                new WorkerDependencyStatusSnapshot("postgres", "degraded", new DateTimeOffset(2026, 04, 15, 10, 01, 00, TimeSpan.Zero), "connection_failed")
            ],
            JobStatuses =
            [
                new WorkerJobStatusSnapshot(
                    "security_data_cleanup",
                    "degraded",
                    300,
                    true,
                    new DateTimeOffset(2026, 04, 15, 10, 00, 45, TimeSpan.Zero),
                    new DateTimeOffset(2026, 04, 15, 10, 01, 00, TimeSpan.Zero),
                    new DateTimeOffset(2026, 04, 15, 10, 00, 30, TimeSpan.Zero),
                    1,
                    1,
                    1,
                    "execution_failed",
                    "execution_failed",
                    [new WorkerJobMetricSnapshot("deletedTotal", 4)])
            ]
        };

        await publisher.PublishAsync(firstSnapshot, CancellationToken.None);
        await publisher.PublishAsync(secondSnapshot, CancellationToken.None);

        var persistedSnapshot = JsonSerializer.Deserialize<WorkerHeartbeatSnapshot>(
            await File.ReadAllTextAsync(heartbeatFilePath),
            SerializerOptions);

        Assert.NotNull(persistedSnapshot);
        Assert.Equal(secondSnapshot.ServiceName, persistedSnapshot!.ServiceName);
        Assert.Equal(secondSnapshot.LastHeartbeatUtc, persistedSnapshot.LastHeartbeatUtc);
        Assert.Equal(secondSnapshot.LastExecutionStartedUtc, persistedSnapshot.LastExecutionStartedUtc);
        Assert.Equal(secondSnapshot.LastExecutionCompletedUtc, persistedSnapshot.LastExecutionCompletedUtc);
        Assert.Equal(secondSnapshot.ExecutionOutcome, persistedSnapshot.ExecutionOutcome);
        Assert.Equal(secondSnapshot.ConsecutiveFailureCount, persistedSnapshot.ConsecutiveFailureCount);
        Assert.Equal(secondSnapshot.ProcessId, persistedSnapshot.ProcessId);
        Assert.Equal(secondSnapshot.DependencyStatuses, persistedSnapshot.DependencyStatuses);
        var jobSnapshot = Assert.Single(persistedSnapshot.JobStatuses);
        Assert.Equal("degraded", jobSnapshot.Status);
        Assert.Equal("execution_failed", jobSnapshot.FailureKind);
        Assert.Equal(1, jobSnapshot.FailedRunCount);
    }

    [Fact]
    public void GetHeartbeatInterval_RejectsNonPositiveInterval()
    {
        var options = new WorkerDiagnosticsOptions
        {
            HeartbeatIntervalSeconds = 0
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.GetHeartbeatInterval());

        Assert.Equal("Worker heartbeat interval must be a positive number of seconds.", exception.Message);
    }

    [Fact]
    public void GetDependencyProbeTimeout_RejectsNonPositiveTimeout()
    {
        var options = new WorkerDiagnosticsOptions
        {
            DependencyProbeTimeoutSeconds = 0
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.GetDependencyProbeTimeout());

        Assert.Equal("Worker dependency probe timeout must be a positive number of seconds.", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static FileWorkerHeartbeatPublisher CreatePublisher(string heartbeatFilePath)
    {
        return new FileWorkerHeartbeatPublisher(Options.Create(new WorkerDiagnosticsOptions
        {
            HeartbeatFilePath = heartbeatFilePath
        }));
    }
}
