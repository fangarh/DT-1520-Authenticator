using System.Text.Json;

namespace Dt1520.Authenticator.ReferenceBackend;

public static class LiveRunPreflightCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static bool ShouldRun(string[] args)
    {
        return args.Any(arg => string.Equals(arg, "--preflight", StringComparison.OrdinalIgnoreCase));
    }

    public static int Run(IConfiguration configuration, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(output);

        var report = LiveRunPreflightReporter.Create(configuration);
        output.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        return report.IsReadyForBackendStart ? 0 : 2;
    }
}
