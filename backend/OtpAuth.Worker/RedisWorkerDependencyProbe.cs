using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace OtpAuth.Worker;

public sealed class RedisWorkerDependencyProbe(
    IConfiguration configuration,
    IOptions<WorkerDiagnosticsOptions> diagnosticsOptions) : IWorkerDependencyProbe
{
    private readonly IConfiguration _configuration = configuration;
    private readonly WorkerDiagnosticsOptions _diagnosticsOptions = diagnosticsOptions.Value;

    public string Name => "redis";

    public async Task<WorkerDependencyProbeResult> CheckAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return WorkerDependencyProbeResult.Unhealthy(Name, "missing_configuration");
        }

        if (!RedisConnectionTarget.TryParse(connectionString, out var target) || target is null)
        {
            return WorkerDependencyProbeResult.Unhealthy(Name, "invalid_configuration");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_diagnosticsOptions.GetDependencyProbeTimeout());

        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(target.Host, target.Port, timeoutSource.Token);
            return WorkerDependencyProbeResult.Healthy(Name);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WorkerDependencyProbeResult.Unhealthy(Name, "timeout");
        }
        catch (SocketException)
        {
            return WorkerDependencyProbeResult.Unhealthy(Name, "connection_failed");
        }
    }
}
