using Dapper;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OtpAuth.Infrastructure.Factors;
using OtpAuth.Infrastructure.Integrations;
using OtpAuth.Infrastructure.Persistence;

var command = args.Length == 0
    ? "migrate"
    : args[0].Trim().ToLowerInvariant();

var connectionString = GetRequiredPostgresConnectionString();

return command switch
{
    "ensure-database" => await EnsureDatabaseAsync(connectionString),
    "migrate" => RunMigrations(connectionString),
    "seed-bootstrap-clients" => await SeedBootstrapClientsAsync(connectionString),
    "seed-bootstrap-totp-enrollment" => await SeedBootstrapTotpEnrollmentAsync(connectionString),
    "reencrypt-totp-secrets" => await ReEncryptTotpSecretsAsync(connectionString),
    "cleanup-security-data" => await CleanupSecurityDataAsync(connectionString),
    "initialize" => await InitializeDatabaseAsync(connectionString),
    "migrate-and-seed-bootstrap-clients" => await MigrateAndSeedBootstrapClientsAsync(connectionString),
    _ => ExitWithUsage(command),
};

static async Task<int> EnsureDatabaseAsync(string connectionString)
{
    var databaseName = PostgresConnectionStringHelper.GetRequiredDatabaseName(connectionString);
    var maintenanceConnectionString = PostgresConnectionStringHelper.BuildMaintenanceConnectionString(connectionString);

    await using var connection = new Npgsql.NpgsqlConnection(maintenanceConnectionString);
    await connection.OpenAsync();

    var exists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
        "select 1 from pg_database where datname = @DatabaseName limit 1;",
        new { DatabaseName = databaseName }));

    if (exists.HasValue)
    {
        Console.WriteLine($"Database '{databaseName}' already exists.");
        return 0;
    }

    var escapedDatabaseName = databaseName.Replace("\"", "\"\"", StringComparison.Ordinal);
    await connection.ExecuteAsync($"create database \"{escapedDatabaseName}\";");

    Console.WriteLine($"Database '{databaseName}' created.");
    return 0;
}

static int RunMigrations(string connectionString)
{
    using var provider = CreateMigrationServiceProvider(connectionString);
    using var scope = provider.CreateScope();

    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();

    Console.WriteLine("PostgreSQL migrations applied successfully.");
    return 0;
}

static async Task<int> SeedBootstrapClientsAsync(string connectionString)
{
    var options = LoadBootstrapOAuthOptions();
    var hasher = new Pbkdf2ClientSecretHasher();
    var factory = new BootstrapIntegrationClientSeedMaterialFactory(hasher);
    var materials = factory.Create(options);

    if (materials.Count == 0)
    {
        Console.WriteLine("No bootstrap integration clients are configured for seeding.");
        return 0;
    }

    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var seeder = new PostgresIntegrationClientSeeder(dataSource);
    await seeder.UpsertAsync(materials, CancellationToken.None);

    Console.WriteLine($"Seeded {materials.Count} bootstrap integration client(s).");
    return 0;
}

static async Task<int> SeedBootstrapTotpEnrollmentAsync(string connectionString)
{
    var bootstrapOAuthOptions = LoadBootstrapOAuthOptions();
    var totpProtectionOptions = LoadTotpProtectionOptions();
    var factory = new BootstrapTotpEnrollmentSeedFactory();
    var material = factory.Create(bootstrapOAuthOptions);

    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var seeder = new PostgresTotpEnrollmentSeeder(dataSource, new TotpSecretProtector(totpProtectionOptions));
    await seeder.UpsertAsync(material, CancellationToken.None);

    Console.WriteLine($"Seeded bootstrap TOTP enrollment for external user '{material.ExternalUserId}'.");
    return 0;
}

static async Task<int> ReEncryptTotpSecretsAsync(string connectionString)
{
    var totpProtectionOptions = LoadTotpProtectionOptions();
    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();

    var service = new TotpSecretsReEncryptionService(
        new PostgresTotpEnrollmentMaintenanceStore(dataSource),
        new TotpSecretProtector(totpProtectionOptions));
    var result = await service.ReEncryptAsync(batchSize: null, CancellationToken.None);

    Console.WriteLine(
        $"TOTP re-encryption completed. Scanned={result.ScannedRecords}, ReEncrypted={result.ReEncryptedRecords}, Skipped={result.SkippedRecords}.");
    return 0;
}

static async Task<int> CleanupSecurityDataAsync(string connectionString)
{
    var retentionOptions = LoadSecurityDataRetentionOptions();
    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();

    var service = new SecurityDataCleanupService(
        new PostgresSecurityDataRetentionStore(dataSource),
        retentionOptions);
    var result = await service.CleanupAsync(DateTimeOffset.UtcNow, CancellationToken.None);

    Console.WriteLine(
        $"Security cleanup completed. ChallengeAttempts={result.DeletedChallengeAttempts}, TotpUsedTimeSteps={result.DeletedExpiredTotpUsedTimeSteps}, RevokedTokens={result.DeletedExpiredRevokedIntegrationAccessTokens}.");
    return 0;
}

static async Task<int> MigrateAndSeedBootstrapClientsAsync(string connectionString)
{
    var migrateExitCode = RunMigrations(connectionString);
    if (migrateExitCode != 0)
    {
        return migrateExitCode;
    }

    return await SeedBootstrapClientsAsync(connectionString);
}

static async Task<int> InitializeDatabaseAsync(string connectionString)
{
    var ensureExitCode = await EnsureDatabaseAsync(connectionString);
    if (ensureExitCode != 0)
    {
        return ensureExitCode;
    }

    return RunMigrations(connectionString);
}

static ServiceProvider CreateMigrationServiceProvider(string connectionString)
{
    var services = new ServiceCollection();
    services
        .AddFluentMigratorCore()
        .ConfigureRunner(runnerBuilder => runnerBuilder
            .AddPostgres()
            .WithGlobalConnectionString(connectionString)
            .ScanIn(typeof(Program).Assembly).For.Migrations());

    return services.BuildServiceProvider(false);
}

static string GetRequiredPostgresConnectionString()
{
    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    throw new InvalidOperationException(
        "Environment variable 'ConnectionStrings__Postgres' is required for PostgreSQL migrations.");
}

static BootstrapOAuthOptions LoadBootstrapOAuthOptions()
{
    var apiProjectPath = ResolveApiProjectPath();
    var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? "Development";

    var configuration = new ConfigurationBuilder()
        .SetBasePath(apiProjectPath)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    return configuration.GetSection("BootstrapOAuth").Get<BootstrapOAuthOptions>()
        ?? new BootstrapOAuthOptions();
}

static TotpProtectionOptions LoadTotpProtectionOptions()
{
    var apiProjectPath = ResolveApiProjectPath();
    var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? "Development";

    var configuration = new ConfigurationBuilder()
        .SetBasePath(apiProjectPath)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    return configuration.GetSection("TotpProtection").Get<TotpProtectionOptions>()
        ?? new TotpProtectionOptions();
}

static SecurityDataRetentionOptions LoadSecurityDataRetentionOptions()
{
    var apiProjectPath = ResolveApiProjectPath();
    var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? "Development";

    var configuration = new ConfigurationBuilder()
        .SetBasePath(apiProjectPath)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    return configuration.GetSection("SecurityRetention").Get<SecurityDataRetentionOptions>()
        ?? new SecurityDataRetentionOptions();
}

static string ResolveApiProjectPath()
{
    var currentDirectoryCandidate = Path.Combine(Directory.GetCurrentDirectory(), "OtpAuth.Api");
    if (Directory.Exists(currentDirectoryCandidate))
    {
        return currentDirectoryCandidate;
    }

    var fallbackCandidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "OtpAuth.Api"));
    if (Directory.Exists(fallbackCandidate))
    {
        return fallbackCandidate;
    }

    throw new DirectoryNotFoundException("Could not resolve the 'OtpAuth.Api' project directory for bootstrap OAuth configuration.");
}

static int ExitWithUsage(string command)
{
    Console.Error.WriteLine($"Unsupported command '{command}'.");
    Console.Error.WriteLine("Supported commands: ensure-database, migrate, initialize, seed-bootstrap-clients, seed-bootstrap-totp-enrollment, reencrypt-totp-secrets, cleanup-security-data, migrate-and-seed-bootstrap-clients");
    return 1;
}
