using Microsoft.Extensions.Logging.Abstractions;
using OtpAuth.Application.Challenges;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class ConfiguredPushChallengeDeliveryGatewayTests
{
    [Fact]
    public async Task DeliverAsync_UsesConfiguredProvider()
    {
        var gateway = new ConfiguredPushChallengeDeliveryGateway(
            [
                new StubProvider(PushChallengeDeliveryProviderNames.Logging, PushChallengeDispatchResult.Failure("logging_selected", false)),
                new StubProvider(PushChallengeDeliveryProviderNames.Fcm, PushChallengeDispatchResult.Success("fcm-message-id")),
            ],
            new PushChallengeDeliveryGatewayOptions
            {
                Provider = PushChallengeDeliveryProviderNames.Fcm,
            },
            NullLogger<ConfiguredPushChallengeDeliveryGateway>.Instance);

        var result = await gateway.DeliverAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("fcm-message-id", result.ProviderMessageId);
    }

    [Fact]
    public async Task DeliverAsync_FailsClosed_WhenConfiguredProviderIsMissing()
    {
        var gateway = new ConfiguredPushChallengeDeliveryGateway(
            [new StubProvider(PushChallengeDeliveryProviderNames.Logging, PushChallengeDispatchResult.Success("logging"))],
            new PushChallengeDeliveryGatewayOptions
            {
                Provider = PushChallengeDeliveryProviderNames.Fcm,
            },
            NullLogger<ConfiguredPushChallengeDeliveryGateway>.Instance);

        var result = await gateway.DeliverAsync(CreateRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("push_provider_not_configured", result.ErrorCode);
        Assert.False(result.IsRetryable);
    }

    private static PushChallengeDispatchRequest CreateRequest()
    {
        return new PushChallengeDispatchRequest
        {
            DeliveryId = Guid.Parse("0d5501f0-165c-4f9d-b956-053a32c339ec"),
            ChallengeId = Guid.Parse("3fe42b05-885e-4328-a489-c3148f2b171f"),
            TargetDeviceId = Guid.Parse("005c7ddf-535b-4be9-a74f-c5d31ff3c710"),
            PushToken = "push-token",
            ExternalUserId = "user-push",
            OperationType = "Login",
            OperationDisplayName = "Approve login",
            CorrelationId = "corr-001",
        };
    }

    private sealed class StubProvider(string providerName, PushChallengeDispatchResult result) : IPushChallengeDeliveryProviderGateway
    {
        public string ProviderName => providerName;

        public Task<PushChallengeDispatchResult> DeliverAsync(
            PushChallengeDispatchRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }
}
