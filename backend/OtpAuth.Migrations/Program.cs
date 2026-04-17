using Dapper;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Administration;
using OtpAuth.Infrastructure.Factors;
using OtpAuth.Infrastructure.Integrations;
using OtpAuth.Infrastructure.Persistence;
using OtpAuth.Infrastructure.Security;

var command = args.Length == 0
    ? "migrate"
    : args[0].Trim().ToLowerInvariant();

return command switch
{
    "ensure-database" => await EnsureDatabaseAsync(GetRequiredPostgresConnectionString()),
    "migrate" => RunMigrations(GetRequiredPostgresConnectionString()),
    "inspect-signing-key-lifecycle" => InspectSigningKeyLifecycle(),
    "audit-signing-key-lifecycle" => await AuditSigningKeyLifecycleAsync(GetRequiredPostgresConnectionString()),
    "list-signing-key-lifecycle-audit-events" => await ListSigningKeyLifecycleAuditEventsAsync(GetRequiredPostgresConnectionString(), args),
    "inspect-totp-protection-key-lifecycle" => await InspectTotpProtectionKeyLifecycleAsync(GetRequiredPostgresConnectionString()),
    "audit-totp-protection-key-lifecycle" => await AuditTotpProtectionKeyLifecycleAsync(GetRequiredPostgresConnectionString()),
    "list-totp-protection-key-lifecycle-audit-events" => await ListTotpProtectionKeyLifecycleAuditEventsAsync(GetRequiredPostgresConnectionString(), args),
    "list-security-audit-events" => await ListSecurityAuditEventsAsync(GetRequiredPostgresConnectionString(), args),
    "list-admin-users" => await ListAdminUsersAsync(GetRequiredPostgresConnectionString()),
    "seed-bootstrap-clients" => await SeedBootstrapClientsAsync(GetRequiredPostgresConnectionString()),
    "seed-bootstrap-totp-enrollment" => await SeedBootstrapTotpEnrollmentAsync(GetRequiredPostgresConnectionString()),
    "seed-bootstrap-backup-codes" => await SeedBootstrapBackupCodesAsync(GetRequiredPostgresConnectionString()),
    "upsert-admin-user" => await UpsertAdminUserAsync(GetRequiredPostgresConnectionString(), args),
    "reencrypt-totp-secrets" => await ReEncryptTotpSecretsAsync(GetRequiredPostgresConnectionString()),
    "cleanup-security-data" => await CleanupSecurityDataAsync(GetRequiredPostgresConnectionString()),
    "rotate-integration-client-secret" => await RotateIntegrationClientSecretAsync(GetRequiredPostgresConnectionString(), args),
    "deactivate-integration-client" => await SetIntegrationClientActiveStateAsync(GetRequiredPostgresConnectionString(), args, isActive: false),
    "activate-integration-client" => await SetIntegrationClientActiveStateAsync(GetRequiredPostgresConnectionString(), args, isActive: true),
    "initialize" => await InitializeDatabaseAsync(GetRequiredPostgresConnectionString()),
    "migrate-and-seed-bootstrap-clients" => await MigrateAndSeedBootstrapClientsAsync(GetRequiredPostgresConnectionString()),
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

static int InspectSigningKeyLifecycle()
{
    var report = CreateSigningKeyLifecycleReport();
    WriteSigningKeyLifecycleReport(report);
    return 0;
}

static async Task<int> AuditSigningKeyLifecycleAsync(string connectionString)
{
    var report = CreateSigningKeyLifecycleReport();
    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var service = new SigningKeyLifecycleAuditService(new SecurityAuditService(new PostgresSecurityAuditStore(dataSource)));
    var auditEvent = await service.RecordSnapshotAsync(report, CancellationToken.None);

    WriteSigningKeyLifecycleReport(report);
    Console.WriteLine($"Audit event recorded: id={auditEvent.EventId}, created_utc={auditEvent.CreatedUtc:O}, severity={auditEvent.Severity}.");
    return 0;
}

static async Task<int> ListSigningKeyLifecycleAuditEventsAsync(string connectionString, string[] args)
{
    var limit = GetOptionalLimitArgument(args);
    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var service = new SigningKeyLifecycleAuditService(new SecurityAuditService(new PostgresSecurityAuditStore(dataSource)));
    var events = await service.ListRecentAsync(limit, CancellationToken.None);

    if (events.Count == 0)
    {
        Console.WriteLine("No signing key lifecycle audit events found.");
        return 0;
    }

    Console.WriteLine($"Recent signing key lifecycle audit events (limit={limit}):");
    foreach (var auditEvent in events)
    {
        Console.WriteLine(
            $"- {auditEvent.CreatedUtc:O} [{auditEvent.EventType}] subject={auditEvent.SubjectId ?? "n/a"}, severity={auditEvent.Severity}, source={auditEvent.Source}, summary={auditEvent.Summary}");
    }

    return 0;
}

static BootstrapSigningKeyLifecycleReport CreateSigningKeyLifecycleReport()
{
    var options = LoadBootstrapOAuthOptions();
    var keyRing = new BootstrapSigningKeyRing(options);
    var reportFactory = new BootstrapSigningKeyLifecycleReportFactory();
    return reportFactory.Create(options, keyRing, DateTimeOffset.UtcNow);
}

static void WriteSigningKeyLifecycleReport(BootstrapSigningKeyLifecycleReport report)
{
    Console.WriteLine($"Observed at: {report.ObservedAtUtc:O}");
    Console.WriteLine($"Current signing key: {report.CurrentSigningKeyId}");
    Console.WriteLine($"Access token lifetime: {report.AccessTokenLifetimeMinutes} minute(s)");
    Console.WriteLine($"Recommended minimum legacy retirement delay after rollout: {TimeSpan.FromSeconds(report.RecommendedLegacyRetirementDelaySeconds):c}");
    Console.WriteLine("Key ring:");

    foreach (var key in report.Keys)
    {
        var role = key.IsCurrent ? "current" : "legacy";
        var validationState = key.IsAcceptedForValidation ? "accepted" : "retired";
        var retireAt = key.RetireAtUtc?.ToString("O") ?? "manual";
        Console.WriteLine($"- {key.KeyId}: role={role}, validation={validationState}, retire_at={retireAt}");
    }

    Console.WriteLine($"Summary: {report.Summary}");
    Console.WriteLine("Operational guidance:");
    Console.WriteLine("- Rollout: set the new key as BootstrapOAuth__CurrentSigningKeyId/CurrentSigningKey.");
    Console.WriteLine("- Move the previous current key into BootstrapOAuth__AdditionalSigningKeys__{n} with RetireAtUtc = rollout time + token lifetime + clock skew.");
    Console.WriteLine("- After RetireAtUtc has passed, remove the retired legacy key from configuration and redeploy.");

    foreach (var warning in report.Warnings)
    {
        Console.WriteLine($"Security warning: {warning}");
    }
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

static async Task<int> SeedBootstrapBackupCodesAsync(string connectionString)
{
    var bootstrapOAuthOptions = LoadBootstrapOAuthOptions();
    var factory = new BootstrapBackupCodeSeedFactory();
    var material = factory.Create(bootstrapOAuthOptions);

    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var seeder = new PostgresBackupCodeSeeder(dataSource, new Pbkdf2BackupCodeHasher());
    await seeder.ReplaceActiveAsync(material, CancellationToken.None);

    Console.WriteLine(
        $"Seeded {material.Codes.Count} bootstrap backup code(s) for external user '{material.ExternalUserId}'.");
    return 0;
}

static async Task<int> InspectTotpProtectionKeyLifecycleAsync(string connectionString)
{
    var report = await CreateTotpProtectionKeyLifecycleReportAsync(connectionString);
    WriteTotpProtectionKeyLifecycleReport(report);
    return 0;
}

static async Task<int> AuditTotpProtectionKeyLifecycleAsync(string connectionString)
{
    var report = await CreateTotpProtectionKeyLifecycleReportAsync(connectionString);
    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var service = new TotpProtectionKeyLifecycleAuditService(new SecurityAuditService(new PostgresSecurityAuditStore(dataSource)));
    var auditEvent = await service.RecordSnapshotAsync(report, CancellationToken.None);

    WriteTotpProtectionKeyLifecycleReport(report);
    Console.WriteLine($"Audit event recorded: id={auditEvent.EventId}, created_utc={auditEvent.CreatedUtc:O}, severity={auditEvent.Severity}.");
    return 0;
}

static async Task<int> ListTotpProtectionKeyLifecycleAuditEventsAsync(string connectionString, string[] args)
{
    var limit = GetOptionalLimitArgument(args);
    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var service = new TotpProtectionKeyLifecycleAuditService(new SecurityAuditService(new PostgresSecurityAuditStore(dataSource)));
    var events = await service.ListRecentAsync(limit, CancellationToken.None);

    if (events.Count == 0)
    {
        Console.WriteLine("No TOTP protection key lifecycle audit events found.");
        return 0;
    }

    Console.WriteLine($"Recent TOTP protection key lifecycle audit events (limit={limit}):");
    foreach (var auditEvent in events)
    {
        Console.WriteLine(
            $"- {auditEvent.CreatedUtc:O} [{auditEvent.EventType}] subject={auditEvent.SubjectId ?? "n/a"}, severity={auditEvent.Severity}, source={auditEvent.Source}, summary={auditEvent.Summary}");
    }

    return 0;
}

static async Task<int> ListSecurityAuditEventsAsync(string connectionString, string[] args)
{
    var limit = GetOptionalLimitArgument(args);
    var eventTypePrefix = GetOptionalEventTypePrefixArgument(args);
    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var service = new SecurityAuditService(new PostgresSecurityAuditStore(dataSource));
    var events = await service.ListRecentAsync(limit, eventTypePrefix, CancellationToken.None);

    if (events.Count == 0)
    {
        Console.WriteLine("No security audit events found.");
        return 0;
    }

    Console.WriteLine(
        eventTypePrefix is null
            ? $"Recent security audit events (limit={limit}):"
            : $"Recent security audit events (limit={limit}, event_type_prefix={eventTypePrefix}):");

    foreach (var auditEvent in events)
    {
        Console.WriteLine(
            $"- {auditEvent.CreatedUtc:O} [{auditEvent.EventType}] subject_type={auditEvent.SubjectType}, subject={auditEvent.SubjectId ?? "n/a"}, severity={auditEvent.Severity}, source={auditEvent.Source}, summary={auditEvent.Summary}");
    }

    return 0;
}

static async Task<int> ListAdminUsersAsync(string connectionString)
{
    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var store = new PostgresAdminUserBootstrapStore(dataSource);
    var users = await store.ListAsync(CancellationToken.None);

    if (users.Count == 0)
    {
        Console.WriteLine("No admin users found.");
        return 0;
    }

    Console.WriteLine("Admin users:");
    foreach (var user in users)
    {
        Console.WriteLine(
            $"- username={user.Username}, active={user.IsActive}, permissions=[{string.Join(", ", user.Permissions)}], id={user.AdminUserId}");
    }

    return 0;
}

static async Task<int> UpsertAdminUserAsync(string connectionString, string[] args)
{
    var username = GetRequiredAdminUsernameArgument(args);
    var password = Environment.GetEnvironmentVariable("OTPAUTH_ADMIN_PASSWORD") ?? string.Empty;
    var permissions = GetRequiredAdminPermissionArguments(args);

    var factory = new AdminUserBootstrapMaterialFactory(new Pbkdf2AdminPasswordHasher());
    var material = factory.Create(username, password, permissions);

    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var store = new PostgresAdminUserBootstrapStore(dataSource);
    var user = await store.UpsertAsync(material, CancellationToken.None);

    Console.WriteLine(
        $"Admin user '{user.Username}' is active with permissions [{string.Join(", ", user.Permissions)}], id={user.AdminUserId}.");
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

static async Task<TotpProtectionKeyLifecycleReport> CreateTotpProtectionKeyLifecycleReportAsync(string connectionString)
{
    var options = LoadTotpProtectionOptions();
    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var usage = await new PostgresTotpEnrollmentMaintenanceStore(dataSource).GetKeyVersionUsageAsync(CancellationToken.None);
    var factory = new TotpProtectionKeyLifecycleReportFactory();
    return factory.Create(options, usage, DateTimeOffset.UtcNow);
}

static void WriteTotpProtectionKeyLifecycleReport(TotpProtectionKeyLifecycleReport report)
{
    Console.WriteLine($"Observed at: {report.ObservedAtUtc:O}");
    Console.WriteLine($"Current TOTP protection key version: {report.CurrentKeyVersion}");
    Console.WriteLine("Configured key versions:");
    foreach (var key in report.ConfiguredKeys)
    {
        var role = key.IsCurrent ? "current" : "legacy";
        Console.WriteLine($"- v{key.KeyVersion}: role={role}");
    }

    Console.WriteLine("Database usage by key version:");
    if (report.UsageByKeyVersion.Count == 0)
    {
        Console.WriteLine("- none");
    }
    else
    {
        foreach (var usage in report.UsageByKeyVersion)
        {
            var state = usage.IsConfigured ? "configured" : "missing_in_runtime";
            var role = usage.IsCurrent ? "current" : "legacy";
            Console.WriteLine($"- v{usage.KeyVersion}: enrollments={usage.EnrollmentCount}, state={state}, role={role}");
        }
    }

    Console.WriteLine($"Re-encryption backlog: {report.EnrollmentsRequiringReEncryptionCount}");
    Console.WriteLine($"Summary: {report.Summary}");
    Console.WriteLine("Operational guidance:");
    Console.WriteLine("- Before removing a legacy TOTP protection key from runtime, re-encrypt remaining enrollments to the current key version.");
    Console.WriteLine("- Use reencrypt-totp-secrets, then inspect/audit the lifecycle report again before retiring legacy key versions from configuration.");

    foreach (var warning in report.Warnings)
    {
        Console.WriteLine($"Security warning: {warning}");
    }
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

static async Task<int> RotateIntegrationClientSecretAsync(string connectionString, string[] args)
{
    var clientId = GetRequiredClientIdArgument(args);
    var explicitClientSecret = Environment.GetEnvironmentVariable("OTPAUTH_NEW_CLIENT_SECRET");

    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var securityAuditService = new SecurityAuditService(new PostgresSecurityAuditStore(dataSource));
    var service = new IntegrationClientLifecycleService(
        new PostgresIntegrationClientLifecycleStore(dataSource),
        new Pbkdf2ClientSecretHasher());
    var result = await service.RotateSecretAsync(clientId, explicitClientSecret, CancellationToken.None);

    if (!result.IsSuccess)
    {
        Console.Error.WriteLine(result.ErrorMessage ?? "Integration client secret rotation failed.");
        return 1;
    }

    try
    {
        await securityAuditService.RecordAsync(
            new IntegrationClientLifecycleAuditFactory().CreateSecretRotatedEntry(
                clientId,
                result.RotatedAtUtc!.Value,
                explicitSecretProvided: !string.IsNullOrWhiteSpace(explicitClientSecret)),
            CancellationToken.None);
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Integration client '{clientId}' secret rotated at {result.RotatedAtUtc:O}.");
        Console.WriteLine($"New client secret: {result.NewClientSecret}");
        Console.Error.WriteLine($"Security audit write failed after secret rotation: {exception.Message}");
        return 1;
    }

    Console.WriteLine($"Integration client '{clientId}' secret rotated at {result.RotatedAtUtc:O}.");
    Console.WriteLine($"New client secret: {result.NewClientSecret}");
    return 0;
}

static async Task<int> SetIntegrationClientActiveStateAsync(string connectionString, string[] args, bool isActive)
{
    var clientId = GetRequiredClientIdArgument(args);

    await using var dataSource = new Npgsql.NpgsqlDataSourceBuilder(connectionString).Build();
    var securityAuditService = new SecurityAuditService(new PostgresSecurityAuditStore(dataSource));
    var service = new IntegrationClientLifecycleService(
        new PostgresIntegrationClientLifecycleStore(dataSource),
        new Pbkdf2ClientSecretHasher());
    var result = await service.SetIsActiveAsync(clientId, isActive, CancellationToken.None);

    if (!result.IsSuccess)
    {
        Console.Error.WriteLine(result.ErrorMessage ?? "Integration client state update failed.");
        return 1;
    }

    var status = isActive ? "activated" : "deactivated";
    try
    {
        await securityAuditService.RecordAsync(
            new IntegrationClientLifecycleAuditFactory().CreateStateChangedEntry(
                clientId,
                isActive,
                result.WasStateChanged,
                result.ChangedAtUtc!.Value),
            CancellationToken.None);
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Integration client '{clientId}' {status} at {result.ChangedAtUtc:O}.");
        Console.Error.WriteLine($"Security audit write failed after integration client state update: {exception.Message}");
        return 1;
    }

    Console.WriteLine($"Integration client '{clientId}' {status} at {result.ChangedAtUtc:O}.");
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

    var backendDirectoryCandidate = Path.Combine(Directory.GetCurrentDirectory(), "backend", "OtpAuth.Api");
    if (Directory.Exists(backendDirectoryCandidate))
    {
        return backendDirectoryCandidate;
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
    Console.Error.WriteLine("Supported commands: ensure-database, migrate, inspect-signing-key-lifecycle, audit-signing-key-lifecycle, list-signing-key-lifecycle-audit-events [limit], inspect-totp-protection-key-lifecycle, audit-totp-protection-key-lifecycle, list-totp-protection-key-lifecycle-audit-events [limit], list-security-audit-events [limit] [event-type-prefix], list-admin-users, initialize, seed-bootstrap-clients, seed-bootstrap-totp-enrollment, seed-bootstrap-backup-codes, upsert-admin-user <username> <permission> [permission...], reencrypt-totp-secrets, cleanup-security-data, rotate-integration-client-secret <client-id>, deactivate-integration-client <client-id>, activate-integration-client <client-id>, migrate-and-seed-bootstrap-clients");
    return 1;
}

static string GetRequiredClientIdArgument(string[] args)
{
    if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
    {
        throw new InvalidOperationException("A non-empty <client-id> argument is required.");
    }

    return args[1].Trim();
}

static string GetRequiredAdminUsernameArgument(string[] args)
{
    if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
    {
        throw new InvalidOperationException("A non-empty <username> argument is required.");
    }

    return args[1].Trim();
}

static IReadOnlyCollection<string> GetRequiredAdminPermissionArguments(string[] args)
{
    if (args.Length < 3)
    {
        throw new InvalidOperationException("At least one <permission> argument is required.");
    }

    return args
        .Skip(2)
        .ToArray();
}

static int GetOptionalLimitArgument(string[] args)
{
    if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
    {
        return 10;
    }

    if (!int.TryParse(args[1], out var limit) || limit <= 0)
    {
        throw new InvalidOperationException("Optional [limit] argument must be a positive integer.");
    }

    return Math.Min(limit, 100);
}

static string? GetOptionalEventTypePrefixArgument(string[] args)
{
    if (args.Length < 3 || string.IsNullOrWhiteSpace(args[2]))
    {
        return null;
    }

    return args[2].Trim();
}
