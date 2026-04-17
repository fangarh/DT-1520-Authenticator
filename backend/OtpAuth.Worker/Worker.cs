using Microsoft.Extensions.Options;

namespace OtpAuth.Worker;

public class Worker(
    ILogger<Worker> logger,
    WorkerDiagnosticsCycleCoordinator diagnosticsCycleCoordinator,
    IOptions<WorkerDiagnosticsOptions> diagnosticsOptions,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private readonly WorkerDiagnosticsCycleCoordinator _diagnosticsCycleCoordinator = diagnosticsCycleCoordinator;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly WorkerDiagnosticsOptions _diagnosticsOptions = diagnosticsOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startedAtUtc = _timeProvider.GetUtcNow();
        var heartbeatInterval = _diagnosticsOptions.GetHeartbeatInterval();
        var diagnosticsState = new WorkerDiagnosticsState
        {
            StartedAtUtc = startedAtUtc,
            ConsecutiveFailureCount = 0,
            JobStates = []
        };

        _logger.LogInformation(
            "OtpAuth.Worker started with heartbeat file '{HeartbeatFilePath}' and interval {HeartbeatIntervalSeconds}s.",
            _diagnosticsOptions.HeartbeatFilePath,
            _diagnosticsOptions.HeartbeatIntervalSeconds);

        diagnosticsState = await RunDiagnosticsCycleAsync(diagnosticsState, stoppingToken);

        using var timer = new PeriodicTimer(heartbeatInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                diagnosticsState = await RunDiagnosticsCycleAsync(diagnosticsState, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("OtpAuth.Worker stopping.");
        }
    }

    private async Task<WorkerDiagnosticsState> RunDiagnosticsCycleAsync(
        WorkerDiagnosticsState diagnosticsState,
        CancellationToken cancellationToken)
    {
        var updatedState = await _diagnosticsCycleCoordinator.RunCycleAsync(diagnosticsState, cancellationToken);
        if (updatedState.ConsecutiveFailureCount > 0)
        {
            _logger.LogWarning(
                "OtpAuth.Worker diagnostics cycle completed with {ConsecutiveFailureCount} consecutive degraded result(s).",
                updatedState.ConsecutiveFailureCount);
        }

        return updatedState;
    }
}
