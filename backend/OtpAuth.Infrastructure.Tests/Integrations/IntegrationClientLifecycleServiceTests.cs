using OtpAuth.Application.Integrations;
using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class IntegrationClientLifecycleServiceTests
{
    [Fact]
    public async Task RotateSecretAsync_GeneratesAndPersistsNewSecret()
    {
        var client = CreateManagedClient();
        var lifecycleStore = new InMemoryLifecycleStore(client);
        var hasher = new Pbkdf2ClientSecretHasher();
        var service = new IntegrationClientLifecycleService(lifecycleStore, hasher);

        var result = await service.RotateSecretAsync(client.ClientId, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.NewClientSecret);
        Assert.True(hasher.Verify(result.NewClientSecret!, lifecycleStore.LastRotatedSecretHash!));
        Assert.NotNull(result.RotatedAtUtc);
        Assert.Equal(result.RotatedAtUtc, lifecycleStore.LastChangedAtUtc);
    }

    [Fact]
    public async Task RotateSecretAsync_ReturnsFailure_WhenClientIsUnknown()
    {
        var service = new IntegrationClientLifecycleService(
            new InMemoryLifecycleStore(),
            new Pbkdf2ClientSecretHasher());

        var result = await service.RotateSecretAsync("missing-client", null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Integration client was not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task SetIsActiveAsync_DeactivatesClientAndAdvancesAuthStateTimestamp()
    {
        var client = CreateManagedClient();
        var lifecycleStore = new InMemoryLifecycleStore(client);
        var service = new IntegrationClientLifecycleService(lifecycleStore, new Pbkdf2ClientSecretHasher());

        var result = await service.SetIsActiveAsync(client.ClientId, false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsActive);
        Assert.True(result.WasStateChanged);
        Assert.False(lifecycleStore.ManagedClient!.IsActive);
        Assert.Equal(result.ChangedAtUtc, lifecycleStore.LastChangedAtUtc);
    }

    [Fact]
    public async Task SetIsActiveAsync_ReturnsExistingTimestamp_WhenStateIsAlreadyApplied()
    {
        var client = CreateManagedClient() with { IsActive = false };
        var lifecycleStore = new InMemoryLifecycleStore(client);
        var service = new IntegrationClientLifecycleService(lifecycleStore, new Pbkdf2ClientSecretHasher());

        var result = await service.SetIsActiveAsync(client.ClientId, false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsActive);
        Assert.False(result.WasStateChanged);
        Assert.Equal(client.LastAuthStateChangedUtc, result.ChangedAtUtc);
        Assert.Null(lifecycleStore.LastChangedAtUtc);
    }

    private static ManagedIntegrationClient CreateManagedClient()
    {
        return new ManagedIntegrationClient
        {
            ClientId = "crm-client",
            IsActive = true,
            LastSecretRotatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            LastAuthStateChangedUtc = DateTimeOffset.UtcNow.AddHours(-1),
        };
    }

    private sealed class InMemoryLifecycleStore : IIntegrationClientLifecycleStore
    {
        public ManagedIntegrationClient? ManagedClient { get; private set; }

        public string? LastRotatedSecretHash { get; private set; }

        public DateTimeOffset? LastChangedAtUtc { get; private set; }

        public InMemoryLifecycleStore(ManagedIntegrationClient? managedClient = null)
        {
            ManagedClient = managedClient;
        }

        public Task<ManagedIntegrationClient?> GetManagedClientByIdAsync(string clientId, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                ManagedClient is not null && string.Equals(ManagedClient.ClientId, clientId, StringComparison.Ordinal)
                    ? ManagedClient
                    : null);
        }

        public Task<bool> RotateSecretAsync(string clientId, string clientSecretHash, DateTimeOffset changedAtUtc, CancellationToken cancellationToken)
        {
            if (ManagedClient is null || !string.Equals(ManagedClient.ClientId, clientId, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            LastRotatedSecretHash = clientSecretHash;
            LastChangedAtUtc = changedAtUtc;
            ManagedClient = ManagedClient with
            {
                LastSecretRotatedUtc = changedAtUtc,
                LastAuthStateChangedUtc = changedAtUtc,
            };
            return Task.FromResult(true);
        }

        public Task<bool> SetIsActiveAsync(string clientId, bool isActive, DateTimeOffset changedAtUtc, CancellationToken cancellationToken)
        {
            if (ManagedClient is null || !string.Equals(ManagedClient.ClientId, clientId, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            LastChangedAtUtc = changedAtUtc;
            ManagedClient = ManagedClient with
            {
                IsActive = isActive,
                LastAuthStateChangedUtc = changedAtUtc,
            };
            return Task.FromResult(true);
        }
    }
}
