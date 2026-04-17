namespace OtpAuth.Worker;

public interface IWorkerHeartbeatPublisher
{
    Task PublishAsync(WorkerHeartbeatSnapshot snapshot, CancellationToken cancellationToken);
}
