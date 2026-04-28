using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminTenantDirectoryHandlerTests
{
    [Fact]
    public async Task QuickCreate_CreatesTenantApplicationAndClient_WithServerGeneratedSecret()
    {
        var store = new RecordingStore();
        var audit = new RecordingAuditWriter();
        var handler = new AdminQuickCreateTenantHandler(
            store,
            new Pbkdf2ClientSecretHasher(),
            new FixedSecretGenerator("server-generated-secret"),
            new FixedIdGenerator(),
            audit);

        var result = await handler.HandleAsync(
            new AdminTenantQuickCreateRequest
            {
                TenantDisplayName = " Example Tenant ",
                ApplicationDisplayName = " Project Manager ",
                IntegrationClientDisplayName = " Backend API ",
                AllowedScopes =
                [
                    IntegrationClientScopes.ChallengesWrite,
                    IntegrationClientScopes.ChallengesRead,
                    IntegrationClientScopes.ChallengesRead,
                ],
            },
            CreateAdminContext(AdminPermissions.TenantsWrite),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Directory);
        Assert.NotNull(result.Client);
        Assert.Equal("server-generated-secret", result.ClientSecret);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), result.Directory!.Tenant.TenantId);
        Assert.Equal("Example Tenant", result.Directory.Tenant.DisplayName);
        Assert.Equal("fixed-client", result.Client!.ClientId);
        Assert.Equal(
            [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.ChallengesWrite],
            result.Client.AllowedScopes);
        Assert.NotNull(store.LastQuickCreateDraft);
        Assert.NotEqual("server-generated-secret", store.LastQuickCreateDraft!.ClientSecretHash);
        Assert.True(new Pbkdf2ClientSecretHasher().Verify("server-generated-secret", store.LastQuickCreateDraft.ClientSecretHash));
        Assert.Single(audit.QuickCreated);
        Assert.Equal("fixed-client", audit.QuickCreated[0].ClientId);
    }

    [Fact]
    public async Task QuickCreate_ReturnsValidationFailed_WhenRequestContainsSecret()
    {
        var handler = CreateQuickCreateHandler(new RecordingStore());

        var result = await handler.HandleAsync(
            ValidQuickCreateRequest() with { HasOperatorProvidedSecret = true },
            CreateAdminContext(AdminPermissions.TenantsWrite),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminQuickCreateTenantErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task QuickCreate_ReturnsAccessDenied_WhenPermissionIsMissing()
    {
        var handler = CreateQuickCreateHandler(new RecordingStore());

        var result = await handler.HandleAsync(
            ValidQuickCreateRequest(),
            CreateAdminContext(AdminPermissions.TenantsRead),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminQuickCreateTenantErrorCode.AccessDenied, result.ErrorCode);
    }

    [Fact]
    public async Task QuickCreate_ReturnsConflict_WhenTenantAlreadyExists()
    {
        var handler = CreateQuickCreateHandler(new RecordingStore { ForceConflict = true });

        var result = await handler.HandleAsync(
            ValidQuickCreateRequest(),
            CreateAdminContext(AdminPermissions.TenantsWrite),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminQuickCreateTenantErrorCode.Conflict, result.ErrorCode);
    }

    private static AdminQuickCreateTenantHandler CreateQuickCreateHandler(RecordingStore store)
    {
        return new AdminQuickCreateTenantHandler(
            store,
            new Pbkdf2ClientSecretHasher(),
            new FixedSecretGenerator("server-generated-secret"),
            new FixedIdGenerator(),
            new RecordingAuditWriter());
    }

    private static AdminTenantQuickCreateRequest ValidQuickCreateRequest()
    {
        return new AdminTenantQuickCreateRequest
        {
            TenantDisplayName = "Example Tenant",
            ApplicationDisplayName = "Project Manager",
            IntegrationClientDisplayName = "Backend API",
            AllowedScopes = [IntegrationClientScopes.ChallengesRead],
        };
    }

    private static AdminContext CreateAdminContext(params string[] permissions)
    {
        return new AdminContext
        {
            AdminUserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Username = "operator",
            Permissions = permissions,
        };
    }

    private sealed class RecordingStore : IAdminTenantDirectoryStore
    {
        public bool ForceConflict { get; init; }

        public AdminTenantQuickCreateDraft? LastQuickCreateDraft { get; private set; }

        public Task<IReadOnlyCollection<AdminTenantDirectoryTenantView>> ListTenantsAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AdminTenantDirectoryDetailView?> GetDirectoryAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AdminTenantDirectoryTenantView?> CreateTenantAsync(AdminTenantCreateDraft draft, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<AdminTenantDirectoryDetailView?> QuickCreateAsync(
            AdminTenantQuickCreateDraft draft,
            CancellationToken cancellationToken)
        {
            LastQuickCreateDraft = draft;
            if (ForceConflict)
            {
                return Task.FromResult<AdminTenantDirectoryDetailView?>(null);
            }

            var tenant = new AdminTenantDirectoryTenantView
            {
                TenantId = draft.TenantId,
                DisplayName = draft.TenantDisplayName,
                Slug = draft.TenantSlug,
                Status = AdminTenantDirectoryStatus.Active,
                ApplicationCount = 1,
                IntegrationClientCount = 1,
                CreatedUtc = draft.CreatedUtc,
                UpdatedUtc = draft.CreatedUtc,
            };
            var application = new AdminTenantDirectoryApplicationView
            {
                ApplicationClientId = draft.ApplicationClientId,
                TenantId = draft.TenantId,
                DisplayName = draft.ApplicationDisplayName,
                Slug = draft.ApplicationSlug,
                Status = AdminTenantDirectoryStatus.Active,
                IntegrationClientCount = 1,
                CreatedUtc = draft.CreatedUtc,
                UpdatedUtc = draft.CreatedUtc,
            };
            var client = new AdminIntegrationClientView
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
            };

            return Task.FromResult<AdminTenantDirectoryDetailView?>(new AdminTenantDirectoryDetailView
            {
                Tenant = tenant,
                Applications = [application],
                IntegrationClients = [client],
            });
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

    private sealed class FixedIdGenerator : IAdminTenantDirectoryIdGenerator
    {
        public Guid NewTenantId()
        {
            return Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        public Guid NewApplicationClientId()
        {
            return Guid.Parse("22222222-2222-2222-2222-222222222222");
        }

        public string NewIntegrationClientId(string tenantDisplayName, string applicationDisplayName, string integrationClientDisplayName)
        {
            return "fixed-client";
        }
    }

    private sealed class RecordingAuditWriter : IAdminTenantDirectoryAuditWriter
    {
        public List<AdminIntegrationClientView> QuickCreated { get; } = [];

        public Task WriteTenantCreatedAsync(
            AdminContext adminContext,
            AdminTenantDirectoryTenantView tenant,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task WriteQuickCreatedAsync(
            AdminContext adminContext,
            AdminTenantDirectoryDetailView directory,
            AdminIntegrationClientView client,
            CancellationToken cancellationToken)
        {
            QuickCreated.Add(client);
            return Task.CompletedTask;
        }
    }
}
