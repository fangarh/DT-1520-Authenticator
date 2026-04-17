using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpAuth.Api.Authentication;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Devices;
using OtpAuth.Infrastructure.Challenges;
using OtpAuth.Infrastructure.Tests.Devices;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class PushChallengeApiTestFactory : WebApplicationFactory<Program>
{
    public const string MissingScopeScenario = TestPushChallengeAuthenticationHandler.MissingScopeScenario;

    static PushChallengeApiTestFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Database=otpauth-tests;Username=test;Password=test");
        Environment.SetEnvironmentVariable("BootstrapOAuth__CurrentSigningKey", new string('t', 32));
        Environment.SetEnvironmentVariable("TotpProtection__CurrentKeyVersion", "1");
        Environment.SetEnvironmentVariable("TotpProtection__CurrentKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=otpauth-tests;Username=test;Password=test",
                ["BootstrapOAuth:CurrentSigningKey"] = new string('t', 32),
                ["DeviceTokens:Issuer"] = "otpauth-device",
                ["DeviceTokens:Audience"] = "otpauth-device-api",
                ["TotpProtection:CurrentKeyVersion"] = "1",
                ["TotpProtection:CurrentKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IChallengeRepository>();
            services.RemoveAll<IChallengeAttemptRecorder>();
            services.RemoveAll<IChallengeDecisionAuditWriter>();
            services.RemoveAll<IDeviceRegistryStore>();

            services.AddSingleton<InMemoryChallengeRepository>();
            services.AddSingleton<RecordingChallengeAttemptRecorder>();
            services.AddSingleton<RecordingChallengeDecisionAuditWriter>();
            services.AddSingleton<InMemoryDeviceRegistryStore>();
            services.AddSingleton<IChallengeRepository>(provider => provider.GetRequiredService<InMemoryChallengeRepository>());
            services.AddSingleton<IChallengeAttemptRecorder>(provider => provider.GetRequiredService<RecordingChallengeAttemptRecorder>());
            services.AddSingleton<IChallengeDecisionAuditWriter>(provider => provider.GetRequiredService<RecordingChallengeDecisionAuditWriter>());
            services.AddSingleton<IDeviceRegistryStore>(provider => provider.GetRequiredService<InMemoryDeviceRegistryStore>());

            services.AddAuthentication(TestPushChallengeAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestPushChallengeAuthenticationHandler>(
                    TestPushChallengeAuthenticationHandler.SchemeName,
                    _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestPushChallengeAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestPushChallengeAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestPushChallengeAuthenticationHandler.SchemeName;
            });
        });
    }

    public HttpClient CreateAuthorizedClient(string scenario = TestPushChallengeAuthenticationHandler.ValidScenario)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestPushChallengeAuthenticationHandler.HeaderName, scenario);
        return client;
    }

    internal InMemoryChallengeRepository GetChallengeRepository()
    {
        return Services.GetRequiredService<InMemoryChallengeRepository>();
    }

    internal InMemoryDeviceRegistryStore GetDeviceStore()
    {
        return Services.GetRequiredService<InMemoryDeviceRegistryStore>();
    }

    internal RecordingChallengeDecisionAuditWriter GetAuditWriter()
    {
        return Services.GetRequiredService<RecordingChallengeDecisionAuditWriter>();
    }

    internal RecordingChallengeAttemptRecorder GetAttemptRecorder()
    {
        return Services.GetRequiredService<RecordingChallengeAttemptRecorder>();
    }

    private sealed class TestPushChallengeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestPushChallenge";
        public const string HeaderName = "X-Test-Auth";
        public const string ValidScenario = "valid";
        public const string MissingScopeScenario = "missing-scope";

        public TestPushChallengeAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var scenarioValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var scenario = scenarioValues.ToString();
            var claims = new List<Claim>
            {
                new("device_id", PushChallengeApiTestContext.DeviceId.ToString()),
                new("tenant_id", PushChallengeApiTestContext.TenantId.ToString()),
                new("application_client_id", PushChallengeApiTestContext.ApplicationClientId.ToString()),
            };

            if (!string.Equals(scenario, MissingScopeScenario, StringComparison.Ordinal))
            {
                claims.Add(new Claim("scope", DeviceTokenScope.Challenge));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
