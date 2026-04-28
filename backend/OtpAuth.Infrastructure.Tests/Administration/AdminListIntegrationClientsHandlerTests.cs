using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminListIntegrationClientsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsClients_WhenRequestIsValid()
    {
        var request = new AdminIntegrationClientListRequest
        {
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        };
        var expectedClient = CreateClient(request.TenantId);
        var store = new StubAdminIntegrationClientStore([expectedClient]);
        var handler = new AdminListIntegrationClientsHandler(store);

        var result = await handler.HandleAsync(
            request,
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.IntegrationClientsRead],
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(request, store.LastRequest);
        Assert.Equal(expectedClient, Assert.Single(result.Clients));
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailed_WhenTenantIdIsMissing()
    {
        var handler = new AdminListIntegrationClientsHandler(new StubAdminIntegrationClientStore([]));

        var result = await handler.HandleAsync(
            new AdminIntegrationClientListRequest
            {
                TenantId = Guid.Empty,
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.IntegrationClientsRead],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListIntegrationClientsErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenPermissionIsMissing()
    {
        var handler = new AdminListIntegrationClientsHandler(new StubAdminIntegrationClientStore([]));

        var result = await handler.HandleAsync(
            new AdminIntegrationClientListRequest
            {
                TenantId = Guid.NewGuid(),
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListIntegrationClientsErrorCode.AccessDenied, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenTenantHasNoClients()
    {
        var handler = new AdminListIntegrationClientsHandler(new StubAdminIntegrationClientStore([]));

        var result = await handler.HandleAsync(
            new AdminIntegrationClientListRequest
            {
                TenantId = Guid.NewGuid(),
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.IntegrationClientsRead],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListIntegrationClientsErrorCode.NotFound, result.ErrorCode);
    }

    private static AdminIntegrationClientView CreateClient(Guid tenantId)
    {
        return new AdminIntegrationClientView
        {
            ClientId = "otpauth-crm",
            TenantId = tenantId,
            ApplicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Status = AdminIntegrationClientStatus.Active,
            AllowedScopes = [IntegrationClientScopes.ChallengesRead, IntegrationClientScopes.ChallengesWrite],
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
            UpdatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            LastSecretRotatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow.AddDays(-1),
        };
    }

    private sealed class StubAdminIntegrationClientStore : IAdminIntegrationClientStore
    {
        private readonly IReadOnlyCollection<AdminIntegrationClientView> _clients;

        public StubAdminIntegrationClientStore(IReadOnlyCollection<AdminIntegrationClientView> clients)
        {
            _clients = clients;
        }

        public AdminIntegrationClientListRequest? LastRequest { get; private set; }

        public Task<IReadOnlyCollection<AdminIntegrationClientView>> ListByTenantAsync(
            AdminIntegrationClientListRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_clients);
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
            throw new NotSupportedException();
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
}
