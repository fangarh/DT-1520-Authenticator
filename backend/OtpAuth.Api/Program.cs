using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging.Console;
using Npgsql;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Factors;
using OtpAuth.Application.Integrations;
using OtpAuth.Application.Policy;
using OtpAuth.Api.Endpoints;
using OtpAuth.Infrastructure.Challenges;
using OtpAuth.Infrastructure.Factors;
using OtpAuth.Infrastructure.Integrations;
using OtpAuth.Infrastructure.Policy;

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
var totpProtectionOptions = builder.Configuration
    .GetSection("TotpProtection")
    .Get<TotpProtectionOptions>() ?? new TotpProtectionOptions();
var clientSecretHasher = new Pbkdf2ClientSecretHasher();
var postgresDataSource = new NpgsqlDataSourceBuilder(postgresConnectionString).Build();
var accessTokenRevocationStore = new PostgresRevokedIntegrationAccessTokenStore(postgresDataSource);
var accessTokenIssuer = new JwtIntegrationAccessTokenIssuer(bootstrapOAuthOptions, accessTokenRevocationStore);
var hasConfiguredHttpsEndpoint =
    (builder.Configuration["ASPNETCORE_URLS"]?.Contains("https://", StringComparison.OrdinalIgnoreCase) ?? false) ||
    !string.IsNullOrWhiteSpace(builder.Configuration["HTTPS_PORT"]) ||
    !string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_HTTPS_PORT"]);

builder.Services.AddOpenApi();
builder.Services.AddSingleton(bootstrapOAuthOptions);
builder.Services.AddSingleton(totpProtectionOptions);
builder.Services.AddSingleton(postgresDataSource);
builder.Services.AddSingleton<IClientSecretHasher>(clientSecretHasher);
builder.Services.AddSingleton<IIntegrationClientStore, PostgresIntegrationClientStore>();
builder.Services.AddSingleton<IIntegrationClientCredentialsValidator, IntegrationClientCredentialsValidator>();
builder.Services.AddSingleton<IIntegrationAccessTokenRevocationStore>(accessTokenRevocationStore);
builder.Services.AddSingleton(accessTokenIssuer);
builder.Services.AddSingleton<IIntegrationAccessTokenIssuer>(accessTokenIssuer);
builder.Services.AddSingleton<IIntegrationAccessTokenIntrospector>(accessTokenIssuer);
builder.Services.AddSingleton<IIntegrationAccessTokenRuntimeValidator, IntegrationAccessTokenRuntimeValidator>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ChallengesRead", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasScope(context.User, IntegrationClientScopes.ChallengesRead)));
    options.AddPolicy("ChallengesWrite", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context => HasScope(context.User, IntegrationClientScopes.ChallengesWrite)));
});
builder.Services.AddSingleton<IssueIntegrationTokenHandler>();
builder.Services.AddSingleton<IntrospectIntegrationTokenHandler>();
builder.Services.AddSingleton<RevokeIntegrationTokenHandler>();
builder.Services.AddSingleton<IPolicyEvaluator, DefaultPolicyEvaluator>();
builder.Services.AddSingleton<IChallengeRepository, PostgresChallengeRepository>();
builder.Services.AddSingleton<IChallengeAttemptRecorder, PostgresChallengeAttemptRecorder>();
builder.Services.AddSingleton<TotpSecretProtector>();
builder.Services.AddSingleton<ITotpEnrollmentStore, PostgresTotpEnrollmentStore>();
builder.Services.AddSingleton<ITotpReplayProtector, PostgresTotpReplayProtector>();
builder.Services.AddSingleton<ITotpVerificationRateLimiter, PostgresTotpVerificationRateLimiter>();
builder.Services.AddSingleton<ITotpVerifier, PostgresTotpVerifier>();
builder.Services.AddSingleton<CreateChallengeHandler>();
builder.Services.AddSingleton<GetChallengeHandler>();
builder.Services.AddSingleton<VerifyTotpHandler>();

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

app.MapAuthEndpoints();
app.MapChallengesEndpoints();

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
