using Npgsql;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Devices;
using OtpAuth.Application.Webhooks;
using OtpAuth.Infrastructure.Challenges;
using OtpAuth.Infrastructure.Devices;
using OtpAuth.Infrastructure.Persistence;
using OtpAuth.Infrastructure.Webhooks;
using OtpAuth.Worker;

var builder = Host.CreateApplicationBuilder(args);
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
}

var challengeCallbackOptions = builder.Configuration
    .GetSection("ChallengeCallbacks")
    .Get<ChallengeCallbackDeliveryGatewayOptions>() ?? new ChallengeCallbackDeliveryGatewayOptions();
var webhookOptions = builder.Configuration
    .GetSection("Webhooks")
    .Get<WebhookDeliveryGatewayOptions>() ?? new WebhookDeliveryGatewayOptions();
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(challengeCallbackOptions.SigningKey))
{
    throw new InvalidOperationException(
        "ChallengeCallbacks:SigningKey must be configured outside Development.");
}

if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(webhookOptions.SigningKey))
{
    throw new InvalidOperationException(
        "Webhooks:SigningKey must be configured outside Development.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(postgresConnectionString).Build());
builder.Services.Configure<WorkerDiagnosticsOptions>(builder.Configuration.GetSection("WorkerDiagnostics"));
builder.Services.Configure<SecurityDataRetentionOptions>(builder.Configuration.GetSection("SecurityRetention"));
builder.Services.Configure<SecurityDataCleanupWorkerJobOptions>(builder.Configuration.GetSection("WorkerJobs:SecurityDataCleanup"));
builder.Services.Configure<PushChallengeDeliveryWorkerJobOptions>(builder.Configuration.GetSection("WorkerJobs:PushChallengeDelivery"));
builder.Services.Configure<WebhookEventDeliveryWorkerJobOptions>(builder.Configuration.GetSection("WorkerJobs:WebhookEventDelivery"));
builder.Services.AddPushChallengeDeliveryServices(builder.Configuration);
builder.Services.AddSingleton(challengeCallbackOptions);
builder.Services.AddSingleton(webhookOptions);
builder.Services.Configure<ChallengeCallbackDeliveryGatewayOptions>(builder.Configuration.GetSection("ChallengeCallbacks"));
builder.Services.Configure<WebhookDeliveryGatewayOptions>(builder.Configuration.GetSection("Webhooks"));
builder.Services.Configure<ChallengeCallbackDeliveryWorkerJobOptions>(builder.Configuration.GetSection("WorkerJobs:ChallengeCallbackDelivery"));
builder.Services.AddSingleton<IWorkerHeartbeatPublisher, FileWorkerHeartbeatPublisher>();
builder.Services.AddSingleton<IWorkerDependencyProbe, PostgresWorkerDependencyProbe>();
builder.Services.AddSingleton<IWorkerDependencyProbe, RedisWorkerDependencyProbe>();
builder.Services.AddSingleton<IChallengeRepository, PostgresChallengeRepository>();
builder.Services.AddSingleton<IDeviceRegistryStore, PostgresDeviceRegistryStore>();
builder.Services.AddSingleton<IChallengeCallbackDeliveryStore, PostgresChallengeCallbackDeliveryStore>();
builder.Services.AddSingleton<IWebhookEventDeliveryStore, PostgresWebhookEventDeliveryStore>();
builder.Services.AddSingleton<ChallengeCallbackDeliveryCoordinator>();
builder.Services.AddSingleton<WebhookEventDeliveryCoordinator>();
builder.Services.AddSingleton<IChallengeCallbackDeliveryGateway, HttpChallengeCallbackDeliveryGateway>();
builder.Services.AddSingleton<IWebhookEventDeliveryGateway, HttpWebhookEventDeliveryGateway>();
builder.Services.AddSingleton<ISecurityDataCleanupRunner, PostgresSecurityDataCleanupRunner>();
builder.Services.AddSingleton<IWorkerJob, SecurityDataCleanupWorkerJob>();
builder.Services.AddSingleton<IWorkerJob, PushChallengeDeliveryWorkerJob>();
builder.Services.AddSingleton<IWorkerJob, ChallengeCallbackDeliveryWorkerJob>();
builder.Services.AddSingleton<IWorkerJob, WebhookEventDeliveryWorkerJob>();
builder.Services.AddSingleton<WorkerDiagnosticsCycleCoordinator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
