namespace OtpAuth.Worker;

public interface IWorkerDependencyProbe
{
    string Name { get; }

    Task<WorkerDependencyProbeResult> CheckAsync(CancellationToken cancellationToken);
}
