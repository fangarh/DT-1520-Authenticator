using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Logging.Console;
using Npgsql;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Devices;
using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Factors;
using OtpAuth.Application.Integrations;
using OtpAuth.Application.Policy;
using OtpAuth.Api.Authentication;
using OtpAuth.Api.Endpoints;
using OtpAuth.Infrastructure.Administration;
using OtpAuth.Infrastructure.Challenges;
using OtpAuth.Infrastructure.Devices;
using OtpAuth.Infrastructure.Factors;
using OtpAuth.Infrastructure.Integrations;
using OtpAuth.Infrastructure.Policy;
using OtpAuth.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
    options.ColorBehavior = LoggerColorBehavior.Disabled;
});

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
}

var bootstrapOAuthOptions = builder.Configuration
    .GetSection("BootstrapOAuth")
    .Get<BootstrapOAuthOptions>() ?? new BootstrapOAuthOptions();
var deviceTokenOptions = builder.Configuration
    .GetSection("DeviceTokens")
    .Get<DeviceTokenOptions>() ?? new DeviceTokenOptions();
if (!builder.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(bootstrapOAuthOptions.CurrentSigningKey) &&
    string.IsNullOrWhiteSpace(bootstrapOAuthOptions.SigningKey))
{
    throw new InvalidOperationException(
        "BootstrapOAuth__CurrentSigningKey must be configured outside Development. Ephemeral signing keys are allowed only for local bootstrap work.");
}

var totpProtectionOptions = builder.Configuration
    .GetSection("TotpProtection")
    .Get<TotpProtectionOptions>() ?? new TotpProtectionOptions();
var clientSecretHasher = new Pbkdf2ClientSecretHasher();
var adminPasswordHasher = new Pbkdf2AdminPasswordHasher();
var postgresDataSource = new NpgsqlDataSourceBuilder(postgresConnectionString).Build();
var integrationClientStore = new PostgresIntegrationClientStore(postgresDataSource);
var adminUserStore = new PostgresAdminUserStore(postgresDataSource);
var accessTokenRevocationStore = new PostgresRevokedIntegrationAccessTokenStore(postgresDataSource);
var deviceAccessTokenIssuer = new JwtDeviceAccessTokenIssuer(deviceTokenOptions, bootstrapOAuthOptions);
var accessTokenIssuer = new JwtIntegrationAccessTokenIssuer(
    bootstrapOAuthOptions,
    integrationClientStore,
    accessTokenRevocationStore);
var hasConfiguredHttpsEndpoint =
    (builder.Configuration["ASPNETCORE_URLS"]?.Contains("https://", StringComparison.OrdinalIgnoreCase) ?? false) ||
    !string.IsNullOrWhiteSpace(builder.Configuration["HTTPS_PORT"]) ||
    !string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_HTTPS_PORT"]);

builder.Services.AddOpenApi();
builder.Services.AddSingleton(bootstrapOAuthOptions);
builder.Services.AddSingleton(deviceTokenOptions);
builder.Services.AddSingleton(totpProtectionOptions);
builder.Services.AddSingleton(postgresDataSource);
builder.Services.AddSingleton<IClientSecretHasher>(clientSecretHasher);
builder.Services.AddSingleton<IAdminPasswordHasher>(adminPasswordHasher);
builder.Services.AddSingleton<IIntegrationClientStore>(integrationClientStore);
builder.Services.AddSingleton<IAdminUserStore>(adminUserStore);
builder.Services.AddSingleton<IIntegrationClientLifecycleStore, PostgresIntegrationClientLifecycleStore>();
builder.Services.AddSingleton<IIntegrationClientCredentialsValidator, IntegrationClientCredentialsValidator>();
builder.Services.AddSingleton<IIntegrationAccessTokenRevocationStore>(accessTokenRevocationStore);
builder.Services.AddSingleton(accessTokenIssuer);
builder.Services.AddSingleton<IIntegrationAccessTokenIssuer>(accessTokenIssuer);
builder.Services.AddSingleton<IIntegrationAccessTokenIntrospector>(accessTokenIssuer);
builder.Services.AddSingleton<IIntegrationAccessTokenRuntimeValidator, IntegrationAccessTokenRuntimeValidator>();
builder.Services.AddSingleton(deviceAccessTokenIssuer);
builder.Services.AddSingleton<IDeviceAccessTokenIssuer>(deviceAccessTokenIssuer);
builder.Services.AddSingleton<IDeviceAccessTokenRuntimeValidator, DeviceAccessTokenRuntimeValidator>();
builder.Services.AddSingleton<IDeviceRegistryStore, PostgresDeviceRegistryStore>();
builder.Services.AddSingleton<IDeviceRefreshTokenHasher, Pbkdf2DeviceRefreshTokenHasher>();
builder.Services.AddSingleton<IDeviceLifecycleAuditWriter, DeviceLifecycleAuditWriter>();
builder.Services.AddSingleton<IntegrationClientLifecycleService>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = AdminAuthenticationDefaults.CompositeScheme;
        options.DefaultAuthenticateScheme = AdminAuthenticationDefaults.CompositeScheme;
        options.DefaultChallengeScheme = AdminAuthenticationDefaults.CompositeScheme;
    })
    .AddPolicyScheme(AdminAuthenticationDefaults.CompositeScheme, AdminAuthenticationDefaults.CompositeScheme, options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Path.StartsWithSegments("/api/v1/admin", StringComparison.OrdinalIgnoreCase)
                ? AdminAuthenticationDefaults.AuthenticationScheme
                : IsDeviceChallengeDecisionPath(context.Request.Path)
                    ? DeviceAuthenticationDefaults.AuthenticationScheme
                : JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = accessTokenIssuer.CreateTokenValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<IIntegrationAccessTokenRuntimeValidator>();
                var result = await validator.ValidateAsync(context.Principal!, context.HttpContext.RequestAborted);
                if (!result.IsValid)
                {
                    context.Fail(result.ErrorMessage ?? "Integration access token validation failed.");
                }
            },
        };
    })
    .AddJwtBearer(DeviceAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = deviceAccessTokenIssuer.CreateTokenValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<IDeviceAccessTokenRuntimeValidator>();
                var result = await validator.ValidateAsync(context.Principal!, context.HttpContext.RequestAborted);
                if (!result.IsValid)
                {
                    context.Fail(result.ErrorMessage ?? "Device access token validation failed.");
                }
            },
        };
    })
    .AddCookie(AdminAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "otpauth-admin-session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "otpauth-admin-csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ChallengesRead", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasScope(context.User, IntegrationClientScopes.ChallengesRead)));
    options.AddPolicy("ChallengesWrite", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasScope(context.User, IntegrationClientScopes.ChallengesWrite)));
    options.AddPolicy("EnrollmentsWrite", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasScope(context.User, IntegrationClientScopes.EnrollmentsWrite)));
    options.AddPolicy("DevicesWrite", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasScope(context.User, IntegrationClientScopes.DevicesWrite)));
    options.AddPolicy("DeviceChallengeWrite", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasScope(context.User, DeviceTokenScope.Challenge)));
    options.AddPolicy(AdminAuthenticationDefaults.AuthenticatedPolicy, policy =>
        policy.RequireAuthenticatedUser());
    options.AddPolicy(AdminAuthenticationDefaults.EnrollmentsReadPolicy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(AdminClaimTypes.Permission, AdminPermissions.EnrollmentsRead));
    options.AddPolicy(AdminAuthenticationDefaults.EnrollmentsWritePolicy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(AdminClaimTypes.Permission, AdminPermissions.EnrollmentsWrite));
});
builder.Services.AddSingleton<IAdminLoginRateLimiter, InMemoryAdminLoginRateLimiter>();
builder.Services.AddSingleton<IAdminAuthAuditWriter, AdminSecurityAuditWriter>();
builder.Services.AddSingleton<IAdminTotpEnrollmentAuditWriter, AdminTotpEnrollmentAuditWriter>();
builder.Services.AddSingleton<IAdminApplicationClientResolver, AdminApplicationClientResolver>();
builder.Services.AddSingleton<AdminLoginHandler>();
builder.Services.AddSingleton<AdminStartTotpEnrollmentHandler>();
builder.Services.AddSingleton<AdminConfirmTotpEnrollmentHandler>();
builder.Services.AddSingleton<AdminReplaceTotpEnrollmentHandler>();
builder.Services.AddSingleton<AdminRevokeTotpEnrollmentHandler>();
builder.Services.AddSingleton<IssueIntegrationTokenHandler>();
builder.Services.AddSingleton<IntrospectIntegrationTokenHandler>();
builder.Services.AddSingleton<RevokeIntegrationTokenHandler>();
builder.Services.AddSingleton<IPolicyEvaluator, DefaultPolicyEvaluator>();
builder.Services.AddSingleton<ISecurityAuditStore, PostgresSecurityAuditStore>();
builder.Services.AddSingleton<SecurityAuditService>();
builder.Services.AddSingleton<IChallengeRepository, PostgresChallengeRepository>();
builder.Services.AddSingleton<IChallengeAttemptRecorder, PostgresChallengeAttemptRecorder>();
builder.Services.AddSingleton<IPushChallengeDeliveryStore, PostgresPushChallengeDeliveryStore>();
builder.Services.AddSingleton<IPushChallengeDeliveryGateway, LoggingPushChallengeDeliveryGateway>();
builder.Services.AddSingleton<PushChallengeDeliveryCoordinator>();
builder.Services.AddSingleton(new Pbkdf2BackupCodeHasher());
builder.Services.AddSingleton<IBackupCodeStore, PostgresBackupCodeStore>();
builder.Services.AddSingleton<IBackupCodeVerificationRateLimiter, PostgresBackupCodeVerificationRateLimiter>();
builder.Services.AddSingleton<IBackupCodeVerifier, PostgresBackupCodeVerifier>();
builder.Services.AddSingleton<TotpSecretProtector>();
builder.Services.AddSingleton<ITotpEnrollmentStore, PostgresTotpEnrollmentStore>();
builder.Services.AddSingleton<ITotpEnrollmentProvisioningStore, PostgresTotpEnrollmentProvisioningStore>();
builder.Services.AddSingleton<ITotpEnrollmentAuditWriter, TotpEnrollmentAuditWriter>();
builder.Services.AddSingleton<ITotpReplayProtector, PostgresTotpReplayProtector>();
builder.Services.AddSingleton<ITotpVerificationRateLimiter, PostgresTotpVerificationRateLimiter>();
builder.Services.AddSingleton<ITotpVerifier, PostgresTotpVerifier>();
builder.Services.AddSingleton<GetTotpEnrollmentHandler>();
builder.Services.AddSingleton<GetCurrentTotpEnrollmentForAdminHandler>();
builder.Services.AddSingleton<ReplaceTotpEnrollmentHandler>();
builder.Services.AddSingleton<RevokeTotpEnrollmentHandler>();
builder.Services.AddSingleton<StartTotpEnrollmentHandler>();
builder.Services.AddSingleton<ConfirmTotpEnrollmentHandler>();
builder.Services.AddSingleton<CreateChallengeHandler>();
builder.Services.AddSingleton<GetChallengeHandler>();
builder.Services.AddSingleton<VerifyBackupCodeHandler>();
builder.Services.AddSingleton<VerifyTotpHandler>();
builder.Services.AddSingleton<IChallengeDecisionAuditWriter, PushChallengeDecisionAuditWriter>();
builder.Services.AddSingleton<ApprovePushChallengeHandler>();
builder.Services.AddSingleton<DenyPushChallengeHandler>();
builder.Services.AddSingleton<ActivateDeviceHandler>();
builder.Services.AddSingleton<ListDevicesForRoutingHandler>();
builder.Services.AddSingleton<RefreshDeviceTokenHandler>();
builder.Services.AddSingleton<RevokeDeviceHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (hasConfiguredHttpsEndpoint)
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapAdminAuthEndpoints();
app.MapAdminEnrollmentReadEndpoints();
app.MapAdminEnrollmentCommandEndpoints();
app.MapAuthEndpoints();
app.MapChallengesEndpoints();
app.MapDevicesEndpoints();
app.MapEnrollmentsEndpoints();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "OtpAuth.Api",
}))
.WithName("HealthCheck");

app.MapGet("/api/v1/system/info", () => Results.Ok(new
{
    service = "OtpAuth.Api",
    version = "0.1.0-scaffold",
    timestampUtc = DateTimeOffset.UtcNow,
}))
.WithName("SystemInfo");

app.Run();

static bool HasScope(ClaimsPrincipal user, string requiredScope)
{
    var scopeValue = user.FindFirst("scope")?.Value;
    if (string.IsNullOrWhiteSpace(scopeValue))
    {
        return false;
    }

    return scopeValue
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Contains(requiredScope, StringComparer.Ordinal);
}

static bool IsDeviceChallengeDecisionPath(PathString path)
{
    if (!path.StartsWithSegments("/api/v1/challenges", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var value = path.Value;
    return value is not null &&
           (value.EndsWith("/approve", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("/deny", StringComparison.OrdinalIgnoreCase));
}

public partial class Program;
