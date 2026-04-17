namespace OtpAuth.Worker;

public sealed class WorkerDiagnosticsOptions
{
    public string HeartbeatFilePath { get; init; } = CreateDefaultHeartbeatFilePath();

    public int HeartbeatIntervalSeconds { get; init; } = 30;

    public int DependencyProbeTimeoutSeconds { get; init; } = 5;

    public TimeSpan GetHeartbeatInterval()
    {
        if (HeartbeatIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("Worker heartbeat interval must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(HeartbeatIntervalSeconds);
    }

    public TimeSpan GetDependencyProbeTimeout()
    {
        if (DependencyProbeTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Worker dependency probe timeout must be a positive number of seconds.");
        }

        return TimeSpan.FromSeconds(DependencyProbeTimeoutSeconds);
    }

    public static string CreateDefaultHeartbeatFilePath()
    {
        return Path.Combine(Path.GetTempPath(), "otpauth-worker", "heartbeat.json");
    }
}
