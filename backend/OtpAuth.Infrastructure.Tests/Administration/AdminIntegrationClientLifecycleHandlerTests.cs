using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminIntegrationClientLifecycleHandlerTests
{
    [Fact]
    public async Task RotateSecretAsync_ReturnsServerGeneratedSecret_AndAdvancesAuthStateTimestamp()
    {
        var existingClient = CreateClient(AdminIntegrationClientStatus.Active);
        var store = new RecordingStore(existingClient);
        var audit = new RecordingAuditWriter();
        var handler = new AdminRotateIntegrationClientSecretHandler(
            store,
            new Pbkdf2ClientSecretHasher(),
            new FixedSecretGenerator("rotated-secret"),
            audit);

        var result = await handler.HandleAsync(
            RouteRequest(existingClient),
            AdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("rotated-secret", result.ClientSecret);
        Assert.NotNull(result.Client);
        Assert.True(result.Client!.LastAuthStateChangedUtc > existingClient.LastAuthStateChangedUtc);
        Assert.Equal(result.Client.LastAuthStateChangedUtc, result.Client.LastSecretRotatedUtc);
        Assert.True(new Pbkdf2ClientSecretHasher().Verify("rotated-secret", store.LastSecretHash!));
        Assert.Single(audit.SecretRotatedClients);
    }

    [Fact]
    public async Task UpdateScopesAsync_RejectsUnsupportedScopes()
    {
        var handler = new AdminUpdateIntegrationClientScopesHandler(
            new RecordingStore(CreateClient(AdminIntegrationClientStatus.Active)),
            new RecordingAuditWriter());

        var result = await handler.HandleAsync(
            new AdminIntegrationClientUpdateScopesRequest
            {
                TenantId = TenantId,
                ClientId = "otpauth-crm",
                AllowedScopes = [IntegrationClientScopes.ChallengesRead, "unsupported:scope"],
            },
            AdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminUpdateIntegrationClientScopesErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateScopesAsync_PersistsWhitelistedScopes_AndAdvancesAuthStateTimestamp()
    {
        var existingClient = CreateClient(AdminIntegrationClientStatus.Active);
        var store = new RecordingStore(existingClient);
        var audit = new RecordingAuditWriter();
        var handler = new AdminUpdateIntegrationClientScopesHandler(store, audit);

        var result = await handler.HandleAsync(
            new AdminIntegrationClientUpdateScopesRequest
            {
                TenantId = existingClient.TenantId,
                ClientId = existingClient.ClientId,
                AllowedScopes = [IntegrationClientScopes.DevicesWrite, IntegrationClientScopes.ChallengesRead],
            },
            AdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Client);
        Assert.Equal(
            [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.DevicesWrite],
            result.Client!.AllowedScopes);
        Assert.True(result.Client.LastAuthStateChangedUtc > existingClient.LastAuthStateChangedUtc);
        Assert.Single(audit.ScopesChangedClients);
    }

    [Fact]
    public async Task SetActiveStateAsync_ReturnsConflict_WhenStateAlreadyMatches()
    {
        var existingClient = CreateClient(AdminIntegrationClientStatus.Inactive);
        var handler = new AdminSetIntegrationClientActiveStateHandler(
            new RecordingStore(existingClient),
            new RecordingAuditWriter());

        var result = await handler.HandleAsync(
            RouteRequest(existingClient),
            isActive: false,
            AdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminSetIntegrationClientActiveStateErrorCode.Conflict, result.ErrorCode);
    }

    [Fact]
    public async Task SetActiveStateAsync_DeactivatesClient_AndAdvancesAuthStateTimestamp()
    {
        var existingClient = CreateClient(AdminIntegrationClientStatus.Active);
        var store = new RecordingStore(existingClient);
        var audit = new RecordingAuditWriter();
        var handler = new AdminSetIntegrationClientActiveStateHandler(store, audit);

        var result = await handler.HandleAsync(
            RouteRequest(existingClient),
            isActive: false,
            AdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Client);
        Assert.Equal(AdminIntegrationClientStatus.Inactive, result.Client!.Status);
        Assert.True(result.Client.LastAuthStateChangedUtc > existingClient.LastAuthStateChangedUtc);
        Assert.Single(audit.DeactivatedClients);
    }

    [Fact]
    public async Task LifecycleHandlers_ReturnAccessDenied_WhenWritePermissionIsMissing()
    {
        var existingClient = CreateClient(AdminIntegrationClientStatus.Active);
        var rotateHandler = new AdminRotateIntegrationClientSecretHandler(
            new RecordingStore(existingClient),
            new Pbkdf2ClientSecretHasher(),
            new FixedSecretGenerator("rotated-secret"),
            new RecordingAuditWriter());

        var result = await rotateHandler.HandleAsync(
            RouteRequest(existingClient),
            AdminContext(AdminPermissions.IntegrationClientsRead),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminRotateIntegrationClientSecretErrorCode.AccessDenied, result.ErrorCode);
    }

    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ApplicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static AdminIntegrationClientView CreateClient(AdminIntegrationClientStatus status)
    {
        return new AdminIntegrationClientView
        {
            ClientId = "otpauth-crm",
            TenantId = TenantId,
            ApplicationClientId = ApplicationClientId,
            Status = status,
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
            UpdatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            LastSecretRotatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow.AddDays(-2),
        };
    }

    private static AdminIntegrationClientRouteRequest RouteRequest(AdminIntegrationClientView client)
    {
        return new AdminIntegrationClientRouteRequest
        {
            TenantId = client.TenantId,
            ClientId = client.ClientId,
        };
    }

    private static AdminContext AdminContext(params string[] permissions)
    {
        return new AdminContext
        {
            AdminUserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Username = "operator",
            Permissions = permissions,
        };
    }

    private sealed class FixedSecretGenerator : IAdminIntegrationClientSecretGenerator
    {
        private readonly string _secret;

        public FixedSecretGenerator(string secret)
        {
            _secret = secret;
        }

        public string Generate()
        {
            return _secret;
        }
    }

    private sealed class RecordingStore : IAdminIntegrationClientStore
    {
        private AdminIntegrationClientView? _client;

        public RecordingStore(AdminIntegrationClientView? client)
        {
            _client = client;
        }

        public string? LastSecretHash { get; private set; }

        public Task<IReadOnlyCollection<AdminIntegrationClientView>> ListByTenantAsync(
            AdminIntegrationClientListRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AdminIntegrationClientView?> GetByTenantAndClientIdAsync(
            Guid tenantId,
            string clientId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Matches(tenantId, clientId) ? _client : null);
        }

        public Task<AdminIntegrationClientView?> CreateAsync(
            AdminIntegrationClientCreateDraft draft,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AdminIntegrationClientView?> RotateSecretAsync(
            Guid tenantId,
            string clientId,
            string clientSecretHash,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken)
        {
            if (!Matches(tenantId, clientId))
            {
                return Task.FromResult<AdminIntegrationClientView?>(null);
            }

            LastSecretHash = clientSecretHash;
            return Task.FromResult(Update(client => client with
            {
                UpdatedUtc = changedAtUtc,
                LastSecretRotatedUtc = changedAtUtc,
                LastAuthStateChangedUtc = changedAtUtc,
            }));
        }

        public Task<AdminIntegrationClientView?> UpdateScopesAsync(
            Guid tenantId,
            string clientId,
            IReadOnlyCollection<string> allowedScopes,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken)
        {
            if (!Matches(tenantId, clientId))
            {
                return Task.FromResult<AdminIntegrationClientView?>(null);
            }

            return Task.FromResult(Update(client => client with
            {
                AllowedScopes = allowedScopes,
                UpdatedUtc = changedAtUtc,
                LastAuthStateChangedUtc = changedAtUtc,
            }));
        }

        public Task<AdminIntegrationClientView?> SetIsActiveAsync(
            Guid tenantId,
            string clientId,
            bool isActive,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken)
        {
            if (!Matches(tenantId, clientId))
            {
                return Task.FromResult<AdminIntegrationClientView?>(null);
            }

            return Task.FromResult(Update(client => client with
            {
                Status = isActive
                    ? AdminIntegrationClientStatus.Active
                    : AdminIntegrationClientStatus.Inactive,
                UpdatedUtc = changedAtUtc,
                LastAuthStateChangedUtc = changedAtUtc,
            }));
        }

        private bool Matches(Guid tenantId, string clientId)
        {
            return _client is not null &&
                   _client.TenantId == tenantId &&
                   string.Equals(_client.ClientId, clientId, StringComparison.Ordinal);
        }

        private AdminIntegrationClientView? Update(Func<AdminIntegrationClientView, AdminIntegrationClientView> update)
        {
            if (_client is null)
            {
                return null;
            }

            _client = update(_client);
            return _client;
        }
    }

    private sealed class RecordingAuditWriter : IAdminIntegrationClientAuditWriter
    {
        public List<AdminIntegrationClientView> SecretRotatedClients { get; } = [];
        public List<AdminIntegrationClientView> ScopesChangedClients { get; } = [];
        public List<AdminIntegrationClientView> DeactivatedClients { get; } = [];

        public Task WriteCreatedAsync(
            AdminContext adminContext,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task WriteSecretRotatedAsync(
            AdminContext adminContext,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            SecretRotatedClients.Add(client);
            return Task.CompletedTask;
        }

        public Task WriteScopesChangedAsync(
            AdminContext adminContext,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            ScopesChangedClients.Add(client);
            return Task.CompletedTask;
        }

        public Task WriteDeactivatedAsync(
            AdminContext adminContext,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            DeactivatedClients.Add(client);
            return Task.CompletedTask;
        }

        public Task WriteReactivatedAsync(
            AdminContext adminContext,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
