using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Administration;
using OtpAuth.Infrastructure.Tests.Enrollments;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminAuthApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _environmentName;

    static AdminAuthApiTestFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Database=otpauth-tests;Username=test;Password=test");
        Environment.SetEnvironmentVariable("BootstrapOAuth__CurrentSigningKey", new string('t', 32));
        Environment.SetEnvironmentVariable("TotpProtection__CurrentKeyVersion", "1");
        Environment.SetEnvironmentVariable("TotpProtection__CurrentKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public AdminAuthApiTestFactory(string environmentName = "Development")
    {
        _environmentName = environmentName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
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
            services.RemoveAll<IAdminUserStore>();
            services.RemoveAll<IAdminAuthAuditWriter>();
            services.RemoveAll<IAdminTotpEnrollmentAuditWriter>();
            services.RemoveAll<IAdminLoginRateLimiter>();
            services.RemoveAll<IIntegrationClientStore>();
            services.RemoveAll<ITotpEnrollmentProvisioningStore>();
            services.RemoveAll<ITotpEnrollmentAuditWriter>();

            services.AddSingleton<InMemoryAdminUserStore>();
            services.AddSingleton<RecordingAdminAuthAuditWriter>();
            services.AddSingleton<RecordingAdminTotpEnrollmentAuditWriter>();
            services.AddSingleton<RecordingTotpEnrollmentAuditWriter>();
            services.AddSingleton<InMemoryIntegrationClientStore>();
            services.AddSingleton<InMemoryEnrollmentApiStore>();
            services.AddSingleton<IAdminUserStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryAdminUserStore>());
            services.AddSingleton<IAdminAuthAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingAdminAuthAuditWriter>());
            services.AddSingleton<IAdminTotpEnrollmentAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingAdminTotpEnrollmentAuditWriter>());
            services.AddSingleton<IAdminLoginRateLimiter, InMemoryAdminLoginRateLimiter>();
            services.AddSingleton<IIntegrationClientStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryIntegrationClientStore>());
            services.AddSingleton<ITotpEnrollmentProvisioningStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryEnrollmentApiStore>());
            services.AddSingleton<ITotpEnrollmentAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingTotpEnrollmentAuditWriter>());
        });
    }

    public HttpClient CreateAdminClient(bool useHttps = false)
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(useHttps ? "https://localhost" : "http://localhost"),
            HandleCookies = true,
        });
    }

    public InMemoryAdminUserStore GetAdminUsers()
    {
        return Services.GetRequiredService<InMemoryAdminUserStore>();
    }

    public RecordingAdminAuthAuditWriter GetAuditWriter()
    {
        return Services.GetRequiredService<RecordingAdminAuthAuditWriter>();
    }

    public InMemoryEnrollmentApiStore GetEnrollments()
    {
        return Services.GetRequiredService<InMemoryEnrollmentApiStore>();
    }

    public InMemoryIntegrationClientStore GetIntegrationClients()
    {
        return Services.GetRequiredService<InMemoryIntegrationClientStore>();
    }

    public RecordingAdminTotpEnrollmentAuditWriter GetAdminEnrollmentAuditWriter()
    {
        return Services.GetRequiredService<RecordingAdminTotpEnrollmentAuditWriter>();
    }

    public RecordingTotpEnrollmentAuditWriter GetTotpEnrollmentAuditWriter()
    {
        return Services.GetRequiredService<RecordingTotpEnrollmentAuditWriter>();
    }

    public sealed class InMemoryAdminUserStore : IAdminUserStore
    {
        private readonly Dictionary<string, AdminUser> _users = new(StringComparer.Ordinal);
        private readonly Pbkdf2AdminPasswordHasher _passwordHasher = new();

        public AdminUser Seed(
            string username,
            string password,
            bool isActive = true,
            params string[] permissions)
        {
            var user = new AdminUser
            {
                AdminUserId = Guid.NewGuid(),
                Username = username,
                NormalizedUsername = username.Trim().ToUpperInvariant(),
                PasswordHash = _passwordHasher.Hash(password),
                IsActive = isActive,
                Permissions = permissions,
            };

            _users[user.NormalizedUsername] = user;
            return user;
        }

        public Task<AdminUser?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _users.TryGetValue(normalizedUsername, out var user);
            return Task.FromResult(user);
        }
    }

    public sealed class RecordingAdminAuthAuditWriter : IAdminAuthAuditWriter
    {
        public List<string> LoginSucceededUsernames { get; } = [];
        public List<(string Username, bool IsRateLimited)> LoginFailures { get; } = [];
        public List<string> LogoutUsernames { get; } = [];

        public Task WriteLoginSucceededAsync(AdminAuthenticatedUser user, string? remoteAddress, CancellationToken cancellationToken)
        {
            LoginSucceededUsernames.Add(user.Username);
            return Task.CompletedTask;
        }

        public Task WriteLoginFailedAsync(string normalizedUsername, string? remoteAddress, Guid? adminUserId, bool isRateLimited, int? retryAfterSeconds, CancellationToken cancellationToken)
        {
            LoginFailures.Add((normalizedUsername, isRateLimited));
            return Task.CompletedTask;
        }

        public Task WriteLogoutAsync(AdminAuthenticatedUser user, string? remoteAddress, CancellationToken cancellationToken)
        {
            LogoutUsernames.Add(user.Username);
            return Task.CompletedTask;
        }
    }

    public sealed class RecordingAdminTotpEnrollmentAuditWriter : IAdminTotpEnrollmentAuditWriter
    {
        public List<(string EventType, Guid AdminUserId, Guid EnrollmentId)> Events { get; } = [];

        public Task WriteStartedAsync(AdminContext adminContext, TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            Events.Add(("started", adminContext.AdminUserId, enrollment.EnrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteConfirmedAsync(AdminContext adminContext, TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, bool isReplacement, CancellationToken cancellationToken)
        {
            Events.Add((isReplacement ? "replacement_confirmed" : "confirmed", adminContext.AdminUserId, enrollment.EnrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteRevokedAsync(AdminContext adminContext, TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            Events.Add(("revoked", adminContext.AdminUserId, enrollment.EnrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteReplacementStartedAsync(AdminContext adminContext, TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            Events.Add(("replacement_started", adminContext.AdminUserId, enrollment.EnrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteConfirmationFailedAsync(AdminContext adminContext, Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, bool isReplacement, CancellationToken cancellationToken)
        {
            Events.Add((isReplacement ? "replacement_confirmation_failed" : "confirmation_failed", adminContext.AdminUserId, enrollmentId));
            return Task.CompletedTask;
        }
    }

    public sealed class RecordingTotpEnrollmentAuditWriter : ITotpEnrollmentAuditWriter
    {
        public List<(string EventType, Guid EnrollmentId)> Events { get; } = [];

        public Task WriteStartedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            Events.Add(("started", enrollment.EnrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            Events.Add(("confirmed", enrollment.EnrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteRevokedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            Events.Add(("revoked", enrollment.EnrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteReplacementStartedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, string? label, string issuer, CancellationToken cancellationToken)
        {
            Events.Add(("replacement_started", enrollment.EnrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmedAsync(TotpEnrollmentView enrollment, Guid tenantId, Guid applicationClientId, string externalUserId, CancellationToken cancellationToken)
        {
            Events.Add(("replacement_confirmed", enrollment.EnrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteReplacementConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            Events.Add(("replacement_confirmation_failed", enrollmentId));
            return Task.CompletedTask;
        }

        public Task WriteConfirmationFailedAsync(Guid enrollmentId, Guid tenantId, Guid applicationClientId, string externalUserId, int failedAttempts, bool attemptLimitReached, CancellationToken cancellationToken)
        {
            Events.Add(("confirmation_failed", enrollmentId));
            return Task.CompletedTask;
        }
    }

    public sealed class InMemoryIntegrationClientStore : IIntegrationClientStore
    {
        private readonly Dictionary<string, IntegrationClient> _clients = new(StringComparer.Ordinal);

        public InMemoryIntegrationClientStore()
        {
            Seed("otpauth-crm", EnrollmentApiTestContext.TenantId, EnrollmentApiTestContext.ApplicationClientId);
        }

        public IntegrationClient Seed(string clientId, Guid tenantId, Guid applicationClientId, params string[] scopes)
        {
            var client = new IntegrationClient
            {
                ClientId = clientId,
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                ClientSecretHash = "hash",
                AllowedScopes = scopes.Length == 0
                    ? [IntegrationClientScopes.EnrollmentsWrite]
                    : scopes,
            };

            _clients[clientId] = client;
            return client;
        }

        public Task<IntegrationClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _clients.TryGetValue(clientId, out var client);
            return Task.FromResult(client);
        }

        public Task<IReadOnlyCollection<IntegrationClient>> ListActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clients = _clients.Values
                .Where(client => client.TenantId == tenantId)
                .OrderBy(client => client.ClientId, StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<IntegrationClient>>(clients);
        }
    }
}
