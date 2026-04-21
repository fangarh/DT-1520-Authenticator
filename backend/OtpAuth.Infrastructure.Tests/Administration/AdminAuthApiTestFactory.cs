using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OtpAuth.Application.Administration;
using OtpAuth.Application.Devices;
using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Integrations;
using OtpAuth.Application.Webhooks;
using OtpAuth.Infrastructure.Administration;
using OtpAuth.Infrastructure.Tests.Devices;
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
            services.RemoveAll<IAdminDeviceAuditWriter>();
            services.RemoveAll<IAdminTotpEnrollmentAuditWriter>();
            services.RemoveAll<IAdminWebhookSubscriptionAuditWriter>();
            services.RemoveAll<IAdminLoginRateLimiter>();
            services.RemoveAll<IIntegrationClientStore>();
            services.RemoveAll<ITotpEnrollmentProvisioningStore>();
            services.RemoveAll<ITotpEnrollmentAuditWriter>();
            services.RemoveAll<IDeviceRegistryStore>();
            services.RemoveAll<IDeviceLifecycleAuditWriter>();
            services.RemoveAll<IAdminDeviceStore>();
            services.RemoveAll<IAdminDeliveryStatusStore>();
            services.RemoveAll<IWebhookSubscriptionStore>();

            services.AddSingleton<InMemoryAdminUserStore>();
            services.AddSingleton<RecordingAdminAuthAuditWriter>();
            services.AddSingleton<RecordingAdminDeviceAuditWriter>();
            services.AddSingleton<RecordingAdminTotpEnrollmentAuditWriter>();
            services.AddSingleton<RecordingAdminWebhookSubscriptionAuditWriter>();
            services.AddSingleton<RecordingTotpEnrollmentAuditWriter>();
            services.AddSingleton<InMemoryIntegrationClientStore>();
            services.AddSingleton<InMemoryEnrollmentApiStore>();
            services.AddSingleton<InMemoryDeviceRegistryStore>();
            services.AddSingleton<RecordingDeviceLifecycleAuditWriter>();
            services.AddSingleton<InMemoryAdminDeviceStore>();
            services.AddSingleton<InMemoryAdminDeliveryStatusStore>();
            services.AddSingleton<InMemoryWebhookSubscriptionStore>();
            services.AddSingleton<IAdminUserStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryAdminUserStore>());
            services.AddSingleton<IAdminAuthAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingAdminAuthAuditWriter>());
            services.AddSingleton<IAdminDeviceAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingAdminDeviceAuditWriter>());
            services.AddSingleton<IAdminTotpEnrollmentAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingAdminTotpEnrollmentAuditWriter>());
            services.AddSingleton<IAdminWebhookSubscriptionAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingAdminWebhookSubscriptionAuditWriter>());
            services.AddSingleton<IAdminLoginRateLimiter, InMemoryAdminLoginRateLimiter>();
            services.AddSingleton<IIntegrationClientStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryIntegrationClientStore>());
            services.AddSingleton<ITotpEnrollmentProvisioningStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryEnrollmentApiStore>());
            services.AddSingleton<ITotpEnrollmentAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingTotpEnrollmentAuditWriter>());
            services.AddSingleton<IDeviceRegistryStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryDeviceRegistryStore>());
            services.AddSingleton<IDeviceLifecycleAuditWriter>(serviceProvider => serviceProvider.GetRequiredService<RecordingDeviceLifecycleAuditWriter>());
            services.AddSingleton<IAdminDeviceStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryAdminDeviceStore>());
            services.AddSingleton<IAdminDeliveryStatusStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryAdminDeliveryStatusStore>());
            services.AddSingleton<IWebhookSubscriptionStore>(serviceProvider => serviceProvider.GetRequiredService<InMemoryWebhookSubscriptionStore>());
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

    public RecordingAdminDeviceAuditWriter GetAdminDeviceAuditWriter()
    {
        return Services.GetRequiredService<RecordingAdminDeviceAuditWriter>();
    }

    public RecordingTotpEnrollmentAuditWriter GetTotpEnrollmentAuditWriter()
    {
        return Services.GetRequiredService<RecordingTotpEnrollmentAuditWriter>();
    }

    public InMemoryWebhookSubscriptionStore GetWebhookSubscriptions()
    {
        return Services.GetRequiredService<InMemoryWebhookSubscriptionStore>();
    }

    public RecordingAdminWebhookSubscriptionAuditWriter GetAdminWebhookSubscriptionAuditWriter()
    {
        return Services.GetRequiredService<RecordingAdminWebhookSubscriptionAuditWriter>();
    }

    internal InMemoryDeviceRegistryStore GetDevices()
    {
        return Services.GetRequiredService<InMemoryDeviceRegistryStore>();
    }

    internal RecordingDeviceLifecycleAuditWriter GetDeviceAuditWriter()
    {
        return Services.GetRequiredService<RecordingDeviceLifecycleAuditWriter>();
    }

    public InMemoryAdminDeliveryStatusStore GetAdminDeliveryStatuses()
    {
        return Services.GetRequiredService<InMemoryAdminDeliveryStatusStore>();
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

    public sealed class RecordingAdminDeviceAuditWriter : IAdminDeviceAuditWriter
    {
        public List<(Guid AdminUserId, Guid DeviceId, string Status, bool IsPushCapable)> Events { get; } = [];

        public Task WriteRevokedAsync(
            AdminContext adminContext,
            Domain.Devices.RegisteredDevice device,
            CancellationToken cancellationToken)
        {
            Events.Add((
                adminContext.AdminUserId,
                device.Id,
                device.Status.ToString().ToLowerInvariant(),
                !string.IsNullOrWhiteSpace(device.PushToken)));
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

    public sealed class RecordingAdminWebhookSubscriptionAuditWriter : IAdminWebhookSubscriptionAuditWriter
    {
        public List<(Guid AdminUserId, Guid SubscriptionId, bool IsActive)> Events { get; } = [];

        public Task WriteSavedAsync(
            AdminContext adminContext,
            WebhookSubscription subscription,
            CancellationToken cancellationToken)
        {
            Events.Add((adminContext.AdminUserId, subscription.SubscriptionId, subscription.IsActive));
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

    public sealed class InMemoryWebhookSubscriptionStore : IWebhookSubscriptionStore
    {
        private readonly Dictionary<(Guid TenantId, Guid ApplicationClientId, string EndpointUrl), WebhookSubscription> _subscriptions = new();

        public WebhookSubscription Seed(WebhookSubscription subscription)
        {
            _subscriptions[(subscription.TenantId, subscription.ApplicationClientId, subscription.EndpointUrl.ToString())] = subscription;
            return subscription;
        }

        public Task<IReadOnlyCollection<WebhookSubscription>> ListAsync(
            WebhookSubscriptionListRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subscriptions = _subscriptions.Values
                .Where(subscription =>
                    (!request.TenantId.HasValue || subscription.TenantId == request.TenantId.Value) &&
                    (!request.ApplicationClientId.HasValue || subscription.ApplicationClientId == request.ApplicationClientId.Value))
                .OrderBy(subscription => subscription.TenantId)
                .ThenBy(subscription => subscription.ApplicationClientId)
                .ThenBy(subscription => subscription.EndpointUrl.ToString(), StringComparer.Ordinal)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<WebhookSubscription>>(subscriptions);
        }

        public Task<WebhookSubscription> UpsertAsync(
            WebhookSubscriptionUpsertRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = (request.TenantId, request.ApplicationClientId, request.EndpointUrl.ToString());
            var now = DateTimeOffset.UtcNow;
            var existing = _subscriptions.GetValueOrDefault(key);
            var subscription = new WebhookSubscription
            {
                SubscriptionId = existing?.SubscriptionId ?? Guid.NewGuid(),
                TenantId = request.TenantId,
                ApplicationClientId = request.ApplicationClientId,
                EndpointUrl = request.EndpointUrl,
                IsActive = request.IsActive,
                EventTypes = request.EventTypes
                    .OrderBy(static item => item, StringComparer.Ordinal)
                    .ToArray(),
                CreatedUtc = existing?.CreatedUtc ?? now,
                UpdatedUtc = existing is null ? now : now,
            };

            _subscriptions[key] = subscription;
            return Task.FromResult(subscription);
        }
    }

    public sealed class InMemoryAdminDeliveryStatusStore : IAdminDeliveryStatusStore
    {
        private readonly List<AdminDeliveryStatusView> _deliveries = [];

        public AdminDeliveryStatusView Seed(AdminDeliveryStatusView delivery)
        {
            _deliveries.Add(delivery);
            return delivery;
        }

        public Task<IReadOnlyCollection<AdminDeliveryStatusView>> ListRecentAsync(
            AdminDeliveryStatusListRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deliveries = _deliveries
                .Where(delivery =>
                    delivery.TenantId == request.TenantId &&
                    (!request.ApplicationClientId.HasValue || delivery.ApplicationClientId == request.ApplicationClientId.Value) &&
                    (!request.Channel.HasValue || delivery.Channel == request.Channel.Value) &&
                    (!request.Status.HasValue || delivery.Status == request.Status.Value))
                .OrderByDescending(delivery => delivery.CreatedAtUtc)
                .ThenByDescending(delivery => delivery.DeliveryId)
                .Take(request.Limit)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<AdminDeliveryStatusView>>(deliveries);
        }
    }

    internal sealed class InMemoryAdminDeviceStore : IAdminDeviceStore
    {
        private readonly InMemoryDeviceRegistryStore _deviceRegistryStore;

        public InMemoryAdminDeviceStore(InMemoryDeviceRegistryStore deviceRegistryStore)
        {
            _deviceRegistryStore = deviceRegistryStore;
        }

        public Task<IReadOnlyCollection<AdminUserDeviceView>> ListByExternalUserAsync(
            AdminUserDeviceListRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var devices = _deviceRegistryStore.ListByTenantAndExternalUser(request.TenantId, request.ExternalUserId)
                .Where(static device => device.Status is Domain.Devices.DeviceStatus.Active or Domain.Devices.DeviceStatus.Revoked or Domain.Devices.DeviceStatus.Blocked)
                .OrderBy(static device => device.Status switch
                {
                    Domain.Devices.DeviceStatus.Active => 0,
                    Domain.Devices.DeviceStatus.Blocked => 1,
                    Domain.Devices.DeviceStatus.Revoked => 2,
                    _ => 3,
                })
                .ThenByDescending(static device => device.LastSeenUtc ?? device.BlockedUtc ?? device.RevokedUtc ?? device.ActivatedUtc ?? device.CreatedUtc)
                .ThenByDescending(static device => device.Id)
                .Select(AdminUserDeviceViewFactory.Create)
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<AdminUserDeviceView>>(devices);
        }
    }
}
