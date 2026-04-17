using Npgsql;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Devices;
using OtpAuth.Infrastructure.Challenges;
using OtpAuth.Infrastructure.Devices;
using OtpAuth.Infrastructure.Persistence;
using OtpAuth.Worker;

var builder = Host.CreateApplicationBuilder(args);
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(postgresConnectionString).Build());
builder.Services.Configure<WorkerDiagnosticsOptions>(builder.Configuration.GetSection("WorkerDiagnostics"));
builder.Services.Configure<SecurityDataRetentionOptions>(builder.Configuration.GetSection("SecurityRetention"));
builder.Services.Configure<SecurityDataCleanupWorkerJobOptions>(builder.Configuration.GetSection("WorkerJobs:SecurityDataCleanup"));
builder.Services.Configure<PushChallengeDeliveryWorkerJobOptions>(builder.Configuration.GetSection("WorkerJobs:PushChallengeDelivery"));
builder.Services.AddSingleton<IWorkerHeartbeatPublisher, FileWorkerHeartbeatPublisher>();
builder.Services.AddSingleton<IWorkerDependencyProbe, PostgresWorkerDependencyProbe>();
builder.Services.AddSingleton<IWorkerDependencyProbe, RedisWorkerDependencyProbe>();
builder.Services.AddSingleton<IChallengeRepository, PostgresChallengeRepository>();
builder.Services.AddSingleton<IDeviceRegistryStore, PostgresDeviceRegistryStore>();
builder.Services.AddSingleton<IPushChallengeDeliveryStore, PostgresPushChallengeDeliveryStore>();
builder.Services.AddSingleton<IPushChallengeDeliveryGateway, LoggingPushChallengeDeliveryGateway>();
builder.Services.AddSingleton<PushChallengeDeliveryCoordinator>();
builder.Services.AddSingleton<ISecurityDataCleanupRunner, PostgresSecurityDataCleanupRunner>();
builder.Services.AddSingleton<IWorkerJob, SecurityDataCleanupWorkerJob>();
builder.Services.AddSingleton<IWorkerJob, PushChallengeDeliveryWorkerJob>();
builder.Services.AddSingleton<WorkerDiagnosticsCycleCoordinator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
