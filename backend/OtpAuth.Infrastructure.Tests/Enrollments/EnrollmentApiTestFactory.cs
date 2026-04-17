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
using OtpAuth.Application.Enrollments;

namespace OtpAuth.Infrastructure.Tests.Enrollments;

public sealed class EnrollmentApiTestFactory : WebApplicationFactory<Program>
{
    public const string MissingScopeScenario = TestEnrollmentAuthenticationHandler.MissingScopeScenario;

    static EnrollmentApiTestFactory()
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
                ["TotpProtection:CurrentKeyVersion"] = "1",
                ["TotpProtection:CurrentKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITotpEnrollmentProvisioningStore>();
            services.RemoveAll<ITotpEnrollmentAuditWriter>();

            services.AddSingleton<InMemoryEnrollmentApiStore>();
            services.AddSingleton<ITotpEnrollmentProvisioningStore>(serviceProvider =>
                serviceProvider.GetRequiredService<InMemoryEnrollmentApiStore>());
            services.AddSingleton<ITotpEnrollmentAuditWriter, NoOpEnrollmentAuditWriter>();

            services.AddAuthentication(TestEnrollmentAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestEnrollmentAuthenticationHandler>(
                    TestEnrollmentAuthenticationHandler.SchemeName,
                    _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestEnrollmentAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestEnrollmentAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestEnrollmentAuthenticationHandler.SchemeName;
            });
        });
    }

    public HttpClient CreateAuthorizedClient(string scenario = TestEnrollmentAuthenticationHandler.ValidScenario)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestEnrollmentAuthenticationHandler.HeaderName, scenario);
        return client;
    }

    public InMemoryEnrollmentApiStore GetStore()
    {
        return Services.GetRequiredService<InMemoryEnrollmentApiStore>();
    }

    private sealed class NoOpEnrollmentAuditWriter : ITotpEnrollmentAuditWriter
    {
        public Task WriteConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteReplacementStartedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteRevokedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task WriteStartedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestEnrollmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestEnrollment";
        public const string HeaderName = "X-Test-Auth";
        public const string ValidScenario = "valid";
        public const string MissingScopeScenario = "missing-scope";

        public TestEnrollmentAuthenticationHandler(
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
                new("client_id", "test-enrollment-client"),
                new("tenant_id", EnrollmentApiTestContext.TenantId.ToString()),
                new("application_client_id", EnrollmentApiTestContext.ApplicationClientId.ToString()),
            };

            if (!string.Equals(scenario, MissingScopeScenario, StringComparison.Ordinal))
            {
                claims.Add(new Claim("scope", "enrollments:write"));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
