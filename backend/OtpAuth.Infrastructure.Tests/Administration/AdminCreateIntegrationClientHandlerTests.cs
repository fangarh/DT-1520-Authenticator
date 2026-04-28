using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminCreateIntegrationClientHandlerTests
{
    [Fact]
    public async Task HandleAsync_CreatesClientWithServerGeneratedSecret_AndWritesAudit()
    {
        var store = new RecordingStore();
        var audit = new RecordingAuditWriter();
        var handler = new AdminCreateIntegrationClientHandler(
            store,
            new Pbkdf2ClientSecretHasher(),
            new FixedSecretGenerator("server-generated-secret"),
            audit);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var applicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var result = await handler.HandleAsync(
            new AdminIntegrationClientCreateRequest
            {
                ClientId = " otpauth-crm ",
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                AllowedScopes =
                [
                    IntegrationClientScopes.ChallengesWrite,
                    IntegrationClientScopes.ChallengesRead,
                    IntegrationClientScopes.ChallengesRead,
                ],
            },
            CreateAdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("server-generated-secret", result.ClientSecret);
        Assert.NotNull(result.Client);
        Assert.Equal("otpauth-crm", result.Client!.ClientId);
        Assert.Equal(
            [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.ChallengesWrite],
            result.Client.AllowedScopes);
        Assert.NotNull(store.LastDraft);
        Assert.Equal("otpauth-crm", store.LastDraft!.ClientId);
        Assert.Equal(tenantId, store.LastDraft.TenantId);
        Assert.Equal(applicationClientId, store.LastDraft.ApplicationClientId);
        Assert.NotEqual("server-generated-secret", store.LastDraft.ClientSecretHash);
        Assert.True(new Pbkdf2ClientSecretHasher().Verify("server-generated-secret", store.LastDraft.ClientSecretHash));
        Assert.Single(audit.CreatedClients);
        Assert.Equal("otpauth-crm", audit.CreatedClients[0].ClientId);
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailed_WhenOperatorProvidesSecret()
    {
        var handler = CreateHandler(new RecordingStore());

        var result = await handler.HandleAsync(
            ValidRequest() with { HasOperatorProvidedSecret = true },
            CreateAdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminCreateIntegrationClientErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailed_WhenScopeIsUnsupported()
    {
        var handler = CreateHandler(new RecordingStore());

        var result = await handler.HandleAsync(
            ValidRequest() with { AllowedScopes = ["challenges:write", "unknown:scope"] },
            CreateAdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminCreateIntegrationClientErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailed_WhenClientIdIsNotRouteSafe()
    {
        var handler = CreateHandler(new RecordingStore());

        var result = await handler.HandleAsync(
            ValidRequest() with { ClientId = "bad/client" },
            CreateAdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminCreateIntegrationClientErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenPermissionIsMissing()
    {
        var handler = CreateHandler(new RecordingStore());

        var result = await handler.HandleAsync(
            ValidRequest(),
            CreateAdminContext(AdminPermissions.IntegrationClientsRead),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminCreateIntegrationClientErrorCode.AccessDenied, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsConflict_WhenClientAlreadyExists()
    {
        var handler = CreateHandler(new RecordingStore { ForceDuplicate = true });

        var result = await handler.HandleAsync(
            ValidRequest(),
            CreateAdminContext(AdminPermissions.IntegrationClientsWrite),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminCreateIntegrationClientErrorCode.Conflict, result.ErrorCode);
    }

    private static AdminCreateIntegrationClientHandler CreateHandler(RecordingStore store)
    {
        return new AdminCreateIntegrationClientHandler(
            store,
            new Pbkdf2ClientSecretHasher(),
            new FixedSecretGenerator("server-generated-secret"),
            new RecordingAuditWriter());
    }

    private static AdminIntegrationClientCreateRequest ValidRequest()
    {
        return new AdminIntegrationClientCreateRequest
        {
            ClientId = "otpauth-crm",
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ApplicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
        };
    }

    private static AdminContext CreateAdminContext(params string[] permissions)
    {
        return new AdminContext
        {
            AdminUserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Username = "operator",
            Permissions = permissions,
        };
    }

    private sealed class RecordingStore : IAdminIntegrationClientStore
    {
        public bool ForceDuplicate { get; init; }

        public AdminIntegrationClientCreateDraft? LastDraft { get; private set; }

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
            throw new NotSupportedException();
        }

        public Task<AdminIntegrationClientView?> CreateAsync(
            AdminIntegrationClientCreateDraft draft,
            CancellationToken cancellationToken)
        {
            LastDraft = draft;
            if (ForceDuplicate)
            {
                return Task.FromResult<AdminIntegrationClientView?>(null);
            }

            return Task.FromResult<AdminIntegrationClientView?>(new AdminIntegrationClientView
            {
                ClientId = draft.ClientId,
                TenantId = draft.TenantId,
                ApplicationClientId = draft.ApplicationClientId,
                Status = AdminIntegrationClientStatus.Active,
                AllowedScopes = draft.AllowedScopes,
                CreatedUtc = draft.CreatedUtc,
                UpdatedUtc = draft.CreatedUtc,
                LastSecretRotatedUtc = draft.CreatedUtc,
                LastAuthStateChangedUtc = draft.CreatedUtc,
            });
        }

        public Task<AdminIntegrationClientView?> RotateSecretAsync(
            Guid tenantId,
            string clientId,
            string clientSecretHash,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AdminIntegrationClientView?> UpdateScopesAsync(
            Guid tenantId,
            string clientId,
            IReadOnlyCollection<string> allowedScopes,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AdminIntegrationClientView?> SetIsActiveAsync(
            Guid tenantId,
            string clientId,
            bool isActive,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
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

    private sealed class RecordingAuditWriter : IAdminIntegrationClientAuditWriter
    {
        public List<AdminIntegrationClientView> CreatedClients { get; } = [];

        public Task WriteCreatedAsync(
            AdminContext adminContext,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            CreatedClients.Add(client);
            return Task.CompletedTask;
        }

        public Task WriteSecretRotatedAsync(
            AdminContext adminContext,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task WriteScopesChangedAsync(
            AdminContext adminContext,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task WriteDeactivatedAsync(
            AdminContext adminContext,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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
