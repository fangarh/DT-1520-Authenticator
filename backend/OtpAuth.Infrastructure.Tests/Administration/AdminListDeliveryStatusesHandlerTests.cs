using OtpAuth.Application.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminListDeliveryStatusesHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsDeliveries_WhenRequestIsValid()
    {
        var request = new AdminDeliveryStatusListRequest
        {
            TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ApplicationClientId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Channel = AdminDeliveryChannel.WebhookEvent,
            Status = AdminDeliveryStatus.Failed,
            Limit = 25,
        };
        var expectedDelivery = new AdminDeliveryStatusView
        {
            DeliveryId = Guid.NewGuid(),
            TenantId = request.TenantId,
            ApplicationClientId = request.ApplicationClientId.Value,
            Channel = AdminDeliveryChannel.WebhookEvent,
            Status = AdminDeliveryStatus.Failed,
            EventType = "device.blocked",
            DeliveryDestination = "https://crm.example.com/webhooks/platform",
            SubjectType = "device",
            SubjectId = Guid.NewGuid(),
            PublicationId = Guid.NewGuid(),
            AttemptCount = 3,
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(1),
            LastAttemptAtUtc = DateTimeOffset.UtcNow,
            LastErrorCode = "delivery_failed",
        };
        var store = new StubAdminDeliveryStatusStore([expectedDelivery]);
        var handler = new AdminListDeliveryStatusesHandler(
            store,
            new StubAdminApplicationClientResolver(AdminApplicationClientResolutionResult.Success(request.ApplicationClientId.Value)));

        var result = await handler.HandleAsync(
            request,
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.WebhooksRead],
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(request, store.LastRequest);
        var actualDelivery = Assert.Single(result.Deliveries);
        Assert.Equal(expectedDelivery, actualDelivery);
    }

    [Fact]
    public async Task HandleAsync_ReturnsValidationFailed_WhenLimitIsOutOfRange()
    {
        var handler = new AdminListDeliveryStatusesHandler(
            new StubAdminDeliveryStatusStore([]),
            new StubAdminApplicationClientResolver(AdminApplicationClientResolutionResult.Success(Guid.NewGuid())));

        var result = await handler.HandleAsync(
            new AdminDeliveryStatusListRequest
            {
                TenantId = Guid.NewGuid(),
                Limit = 0,
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.WebhooksRead],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListDeliveryStatusesErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAccessDenied_WhenPermissionIsMissing()
    {
        var handler = new AdminListDeliveryStatusesHandler(
            new StubAdminDeliveryStatusStore([]),
            new StubAdminApplicationClientResolver(AdminApplicationClientResolutionResult.Success(Guid.NewGuid())));

        var result = await handler.HandleAsync(
            new AdminDeliveryStatusListRequest
            {
                TenantId = Guid.NewGuid(),
                Limit = 10,
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListDeliveryStatusesErrorCode.AccessDenied, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenRequestedApplicationClientDoesNotBelongToTenant()
    {
        var handler = new AdminListDeliveryStatusesHandler(
            new StubAdminDeliveryStatusStore([]),
            new StubAdminApplicationClientResolver(AdminApplicationClientResolutionResult.Failure(
                AdminApplicationClientResolutionErrorCode.NotFound,
                "Application client was not found.")));

        var result = await handler.HandleAsync(
            new AdminDeliveryStatusListRequest
            {
                TenantId = Guid.NewGuid(),
                ApplicationClientId = Guid.NewGuid(),
                Limit = 10,
            },
            new AdminContext
            {
                AdminUserId = Guid.NewGuid(),
                Username = "operator",
                Permissions = [AdminPermissions.WebhooksRead],
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminListDeliveryStatusesErrorCode.NotFound, result.ErrorCode);
    }

    private sealed class StubAdminDeliveryStatusStore : IAdminDeliveryStatusStore
    {
        private readonly IReadOnlyCollection<AdminDeliveryStatusView> _deliveries;

        public StubAdminDeliveryStatusStore(IReadOnlyCollection<AdminDeliveryStatusView> deliveries)
        {
            _deliveries = deliveries;
        }

        public AdminDeliveryStatusListRequest? LastRequest { get; private set; }

        public Task<IReadOnlyCollection<AdminDeliveryStatusView>> ListRecentAsync(
            AdminDeliveryStatusListRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_deliveries);
        }
    }

    private sealed class StubAdminApplicationClientResolver : IAdminApplicationClientResolver
    {
        private readonly AdminApplicationClientResolutionResult _result;

        public StubAdminApplicationClientResolver(AdminApplicationClientResolutionResult result)
        {
            _result = result;
        }

        public Task<AdminApplicationClientResolutionResult> ResolveAsync(
            Guid tenantId,
            Guid? requestedApplicationClientId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }
}
