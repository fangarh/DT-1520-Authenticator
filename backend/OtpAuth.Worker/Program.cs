using OtpAuth.Infrastructure.Persistence;
using OtpAuth.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<WorkerDiagnosticsOptions>(builder.Configuration.GetSection("WorkerDiagnostics"));
builder.Services.Configure<SecurityDataRetentionOptions>(builder.Configuration.GetSection("SecurityRetention"));
builder.Services.Configure<SecurityDataCleanupWorkerJobOptions>(builder.Configuration.GetSection("WorkerJobs:SecurityDataCleanup"));
builder.Services.AddSingleton<IWorkerHeartbeatPublisher, FileWorkerHeartbeatPublisher>();
builder.Services.AddSingleton<IWorkerDependencyProbe, PostgresWorkerDependencyProbe>();
builder.Services.AddSingleton<IWorkerDependencyProbe, RedisWorkerDependencyProbe>();
builder.Services.AddSingleton<ISecurityDataCleanupRunner, PostgresSecurityDataCleanupRunner>();
builder.Services.AddSingleton<IWorkerJob, SecurityDataCleanupWorkerJob>();
builder.Services.AddSingleton<WorkerDiagnosticsCycleCoordinator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
