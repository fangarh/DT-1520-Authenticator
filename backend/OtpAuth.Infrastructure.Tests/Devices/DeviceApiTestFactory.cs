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
using OtpAuth.Application.Devices;

namespace OtpAuth.Infrastructure.Tests.Devices;

public sealed class DeviceApiTestFactory : WebApplicationFactory<Program>
{
    public const string MissingScopeScenario = TestDeviceAuthenticationHandler.MissingScopeScenario;
    public static readonly Guid TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    public static readonly Guid ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");

    static DeviceApiTestFactory()
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
                ["BootstrapOAuth:Issuer"] = "otpauth-bootstrap",
                ["BootstrapOAuth:Audience"] = "otpauth-api",
                ["DeviceTokens:Issuer"] = "otpauth-device",
                ["DeviceTokens:Audience"] = "otpauth-device-api",
                ["DeviceTokens:AccessTokenLifetimeMinutes"] = "15",
                ["DeviceTokens:RefreshTokenLifetimeDays"] = "30",
                ["TotpProtection:CurrentKeyVersion"] = "1",
                ["TotpProtection:CurrentKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDeviceRegistryStore>();
            services.RemoveAll<IDeviceLifecycleAuditWriter>();

            services.AddSingleton<InMemoryDeviceRegistryStore>();
            services.AddSingleton<RecordingDeviceLifecycleAuditWriter>();
            services.AddSingleton<IDeviceRegistryStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryDeviceRegistryStore>());
            services.AddSingleton<IDeviceLifecycleAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingDeviceLifecycleAuditWriter>());

            services.AddAuthentication(TestDeviceAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestDeviceAuthenticationHandler>(
                    TestDeviceAuthenticationHandler.SchemeName,
                    _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestDeviceAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestDeviceAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestDeviceAuthenticationHandler.SchemeName;
            });
        });
    }

    public HttpClient CreateAuthorizedClient(string scenario = TestDeviceAuthenticationHandler.ValidScenario)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestDeviceAuthenticationHandler.HeaderName, scenario);
        return client;
    }

    internal InMemoryDeviceRegistryStore GetStore()
    {
        return Services.GetRequiredService<InMemoryDeviceRegistryStore>();
    }

    internal RecordingDeviceLifecycleAuditWriter GetAuditWriter()
    {
        return Services.GetRequiredService<RecordingDeviceLifecycleAuditWriter>();
    }

    private sealed class TestDeviceAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestDevices";
        public const string HeaderName = "X-Test-Auth";
        public const string ValidScenario = "valid";
        public const string MissingScopeScenario = "missing-scope";

        public TestDeviceAuthenticationHandler(
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
                new("client_id", "test-device-client"),
                new("tenant_id", TenantId.ToString()),
                new("application_client_id", ApplicationClientId.ToString()),
            };

            if (!string.Equals(scenario, MissingScopeScenario, StringComparison.Ordinal))
            {
                claims.Add(new Claim("scope", "devices:write"));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
