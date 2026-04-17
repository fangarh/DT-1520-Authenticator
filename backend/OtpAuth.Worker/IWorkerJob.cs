namespace OtpAuth.Worker;

public interface IWorkerJob
{
    string Name { get; }

    bool IsEnabled { get; }

    TimeSpan GetInterval();

    Task<WorkerJobRunResult> ExecuteAsync(DateTimeOffset utcNow, CancellationToken cancellationToken);
}
