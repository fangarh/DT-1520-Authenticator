using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OtpAuth.Worker;

public sealed class FileWorkerHeartbeatPublisher(IOptions<WorkerDiagnosticsOptions> options) : IWorkerHeartbeatPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly WorkerDiagnosticsOptions _options = options.Value;

    public async Task PublishAsync(WorkerHeartbeatSnapshot snapshot, CancellationToken cancellationToken)
    {
        var heartbeatDirectoryPath = Path.GetDirectoryName(_options.HeartbeatFilePath);
        if (string.IsNullOrWhiteSpace(heartbeatDirectoryPath))
        {
            throw new InvalidOperationException("Heartbeat file path must include a directory.");
        }

        Directory.CreateDirectory(heartbeatDirectoryPath);

        var temporaryFilePath = Path.Combine(
            heartbeatDirectoryPath,
            $".heartbeat-{Guid.NewGuid():N}.tmp");

        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
        await File.WriteAllTextAsync(temporaryFilePath, json, cancellationToken);
        File.Move(temporaryFilePath, _options.HeartbeatFilePath, true);
    }
}
