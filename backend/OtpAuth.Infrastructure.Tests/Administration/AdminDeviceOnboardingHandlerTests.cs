using OtpAuth.Application.Administration;
using OtpAuth.Application.Devices;
using OtpAuth.Domain.Devices;
using OtpAuth.Infrastructure.Devices;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminDeviceOnboardingHandlerTests
{
    [Fact]
    public async Task CreateAsync_GeneratesOneTimePayload_AndStoresOnlyHash()
    {
        var store = new StubOnboardingStore();
        var audit = new RecordingAuditWriter();
        var handler = new AdminCreateDeviceOnboardingArtifactHandler(
            store,
            new Pbkdf2DeviceRefreshTokenHasher(),
            new FixedSecretGenerator("activation-secret"),
            audit);

        var result = await handler.HandleAsync(
            new AdminDeviceOnboardingCreateRequest
            {
                TenantId = TestTenantId,
                ApplicationClientId = TestApplicationClientId,
                ExternalUserId = " user-123 ",
                Platform = DevicePlatform.Android,
                TtlMinutes = 5,
            },
            WriteAdmin(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Artifact);
        Assert.NotNull(result.ActivationPayload);
        Assert.StartsWith("dac_", result.ActivationPayload!, StringComparison.Ordinal);
        Assert.Equal("user-123", store.CreatedDraft!.ExternalUserId);
        Assert.NotEqual("activation-secret", store.CreatedDraft.CodeHash);
        Assert.DoesNotContain("activation-secret", store.CreatedDraft.CodeHash, StringComparison.Ordinal);
        Assert.Contains(audit.Events, item => item == "created");
    }

    [Fact]
    public async Task CreateAsync_RejectsOperatorProvidedPayload()
    {
        var handler = new AdminCreateDeviceOnboardingArtifactHandler(
            new StubOnboardingStore(),
            new Pbkdf2DeviceRefreshTokenHasher(),
            new FixedSecretGenerator("activation-secret"),
            new RecordingAuditWriter());

        var result = await handler.HandleAsync(
            new AdminDeviceOnboardingCreateRequest
            {
                TenantId = TestTenantId,
                ApplicationClientId = TestApplicationClientId,
                ExternalUserId = "user-123",
                Platform = DevicePlatform.Android,
                HasOperatorProvidedActivationPayload = true,
            },
            WriteAdmin(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminCreateDeviceOnboardingArtifactErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ListAsync_ReturnsAccessDenied_WhenPermissionIsMissing()
    {
        var handler = new AdminListDeviceOnboardingArtifactsHandler(new StubOnboardingStore());

        var result = await handler.HandleAsync(
            new AdminDeviceOnboardingListRequest
            {
                TenantId = TestTenantId,
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListDeviceOnboardingArtifactsErrorCode.AccessDenied, result.ErrorCode);
    }

    [Fact]
    public async Task RevokeAsync_RejectsConsumedArtifactConflict()
    {
        var artifact = CreateView(AdminDeviceOnboardingStatus.Consumed);
        var handler = new AdminRevokeDeviceOnboardingArtifactHandler(
            new StubOnboardingStore(revokedArtifact: artifact),
            new RecordingAuditWriter());

        var result = await handler.HandleAsync(
            new AdminDeviceOnboardingRouteRequest
            {
                TenantId = artifact.TenantId,
                ActivationCodeId = artifact.ActivationCodeId,
            },
            WriteAdmin(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminRevokeDeviceOnboardingArtifactErrorCode.Conflict, result.ErrorCode);
    }

    private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestApplicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static AdminContext WriteAdmin()
    {
        return new AdminContext
        {
            AdminUserId = Guid.NewGuid(),
            Username = "operator",
            Permissions = [AdminPermissions.DevicesRead, AdminPermissions.DevicesWrite],
        };
    }

    private static AdminDeviceOnboardingView CreateView(AdminDeviceOnboardingStatus status)
    {
        return new AdminDeviceOnboardingView
        {
            ActivationCodeId = Guid.NewGuid(),
            TenantId = TestTenantId,
            ApplicationClientId = TestApplicationClientId,
            ExternalUserId = "user-123",
            Platform = DevicePlatform.Android,
            Status = status,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            ConsumedUtc = status == AdminDeviceOnboardingStatus.Consumed ? DateTimeOffset.UtcNow : null,
            RevokedUtc = status == AdminDeviceOnboardingStatus.Revoked ? DateTimeOffset.UtcNow : null,
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
    }

    private sealed class FixedSecretGenerator : IAdminDeviceActivationSecretGenerator
    {
        private readonly string _secret;

        public FixedSecretGenerator(string secret)
        {
            _secret = secret;
        }

        public string Generate() => _secret;
    }

    private sealed class RecordingAuditWriter : IAdminDeviceOnboardingAuditWriter
    {
        public List<string> Events { get; } = [];

        public Task WriteCreatedAsync(AdminContext adminContext, AdminDeviceOnboardingView artifact, CancellationToken cancellationToken)
        {
            Events.Add("created");
            return Task.CompletedTask;
        }

        public Task WriteRevokedAsync(AdminContext adminContext, AdminDeviceOnboardingView artifact, CancellationToken cancellationToken)
        {
            Events.Add("revoked");
            return Task.CompletedTask;
        }
    }

    private sealed class StubOnboardingStore : IAdminDeviceOnboardingStore
    {
        private readonly AdminDeviceOnboardingView? _revokedArtifact;

        public StubOnboardingStore(AdminDeviceOnboardingView? revokedArtifact = null)
        {
            _revokedArtifact = revokedArtifact;
        }

        public AdminDeviceOnboardingCreateDraft? CreatedDraft { get; private set; }

        public Task<IReadOnlyCollection<AdminDeviceOnboardingView>> ListAsync(
            AdminDeviceOnboardingListRequest request,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<AdminDeviceOnboardingView>>([CreateView(AdminDeviceOnboardingStatus.Pending)]);
        }

        public Task<AdminDeviceOnboardingView?> CreateAsync(
            AdminDeviceOnboardingCreateDraft draft,
            CancellationToken cancellationToken)
        {
            CreatedDraft = draft;
            return Task.FromResult<AdminDeviceOnboardingView?>(new AdminDeviceOnboardingView
            {
                ActivationCodeId = draft.ActivationCodeId,
                TenantId = draft.TenantId,
                ApplicationClientId = draft.ApplicationClientId,
                ExternalUserId = draft.ExternalUserId,
                Platform = draft.Platform,
                Status = AdminDeviceOnboardingStatus.Pending,
                ExpiresUtc = draft.ExpiresUtc,
                CreatedUtc = draft.CreatedUtc,
            });
        }

        public Task<AdminDeviceOnboardingRevokeStoreResult> RevokeAsync(
            Guid tenantId,
            Guid activationCodeId,
            DateTimeOffset revokedAtUtc,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminDeviceOnboardingRevokeStoreResult
            {
                IsFound = _revokedArtifact is not null,
                WasRevoked = _revokedArtifact?.Status == AdminDeviceOnboardingStatus.Revoked,
                Artifact = _revokedArtifact,
            });
        }
    }
}
