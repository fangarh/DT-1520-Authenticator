using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using OtpAuth.Infrastructure.Persistence;

namespace OtpAuth.Worker;

public sealed class PostgresSecurityDataCleanupRunner(
    IConfiguration configuration,
    IOptions<SecurityDataRetentionOptions> retentionOptions) : ISecurityDataCleanupRunner
{
    private readonly IConfiguration _configuration = configuration;
    private readonly SecurityDataRetentionOptions _retentionOptions = retentionOptions.Value;

    public async Task<SecurityDataCleanupResult> CleanupAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
        }

        await using var dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
        var service = new SecurityDataCleanupService(
            new PostgresSecurityDataRetentionStore(dataSource),
            _retentionOptions);

        return await service.CleanupAsync(utcNow, cancellationToken);
    }
}
