using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace OtpAuth.Worker;

public sealed class PostgresWorkerDependencyProbe(
    IConfiguration configuration,
    IOptions<WorkerDiagnosticsOptions> diagnosticsOptions) : IWorkerDependencyProbe
{
    private readonly IConfiguration _configuration = configuration;
    private readonly WorkerDiagnosticsOptions _diagnosticsOptions = diagnosticsOptions.Value;

    public string Name => "postgres";

    public async Task<WorkerDependencyProbeResult> CheckAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return WorkerDependencyProbeResult.Unhealthy(Name, "missing_configuration");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_diagnosticsOptions.GetDependencyProbeTimeout());

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(timeoutSource.Token);

            await using var command = new NpgsqlCommand("select 1", connection);
            await command.ExecuteScalarAsync(timeoutSource.Token);

            return WorkerDependencyProbeResult.Healthy(Name);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WorkerDependencyProbeResult.Unhealthy(Name, "timeout");
        }
        catch (NpgsqlException)
        {
            return WorkerDependencyProbeResult.Unhealthy(Name, "connection_failed");
        }
        catch (InvalidOperationException)
        {
            return WorkerDependencyProbeResult.Unhealthy(Name, "invalid_configuration");
        }
    }
}
