namespace OtpAuth.Worker;

public sealed record WorkerDependencyProbeResult(
    string Name,
    bool IsHealthy,
    string? FailureKind)
{
    public static WorkerDependencyProbeResult Healthy(string name) => new(name, true, null);

    public static WorkerDependencyProbeResult Unhealthy(string name, string failureKind) => new(name, false, failureKind);
}
