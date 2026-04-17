using OtpAuth.Application.Administration;
using OtpAuth.Application.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminApplicationClientResolverTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsRequestedClient_WhenItBelongsToTenant()
    {
        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var applicationClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var store = new StubIntegrationClientStore([
            new IntegrationClient
            {
                ClientId = "otpauth-crm",
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                ClientSecretHash = "hash",
            },
        ]);
        var resolver = new AdminApplicationClientResolver(store);

        var result = await resolver.ResolveAsync(tenantId, applicationClientId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(applicationClientId, result.ApplicationClientId);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsSingleTenantClient_WhenRequestDoesNotSpecifyOne()
    {
        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var applicationClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var store = new StubIntegrationClientStore([
            new IntegrationClient
            {
                ClientId = "otpauth-crm",
                TenantId = tenantId,
                ApplicationClientId = applicationClientId,
                ClientSecretHash = "hash",
            },
        ]);
        var resolver = new AdminApplicationClientResolver(store);

        var result = await resolver.ResolveAsync(tenantId, requestedApplicationClientId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(applicationClientId, result.ApplicationClientId);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsConflict_WhenTenantHasMultipleClientsAndRequestIsImplicit()
    {
        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = new StubIntegrationClientStore([
            new IntegrationClient
            {
                ClientId = "otpauth-crm-a",
                TenantId = tenantId,
                ApplicationClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                ClientSecretHash = "hash",
            },
            new IntegrationClient
            {
                ClientId = "otpauth-crm-b",
                TenantId = tenantId,
                ApplicationClientId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                ClientSecretHash = "hash",
            },
        ]);
        var resolver = new AdminApplicationClientResolver(store);

        var result = await resolver.ResolveAsync(tenantId, requestedApplicationClientId: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminApplicationClientResolutionErrorCode.Conflict, result.ErrorCode);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNotFound_WhenTenantHasNoClients()
    {
        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var resolver = new AdminApplicationClientResolver(new StubIntegrationClientStore([]));

        var result = await resolver.ResolveAsync(tenantId, requestedApplicationClientId: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminApplicationClientResolutionErrorCode.NotFound, result.ErrorCode);
    }

    private sealed class StubIntegrationClientStore : IIntegrationClientStore
    {
        private readonly IReadOnlyCollection<IntegrationClient> _clients;

        public StubIntegrationClientStore(IReadOnlyCollection<IntegrationClient> clients)
        {
            _clients = clients;
        }

        public Task<IntegrationClient?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_clients.FirstOrDefault(client => client.ClientId == clientId));
        }

        public Task<IReadOnlyCollection<IntegrationClient>> ListActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<IntegrationClient>>(
                _clients.Where(client => client.TenantId == tenantId).ToArray());
        }
    }
}
