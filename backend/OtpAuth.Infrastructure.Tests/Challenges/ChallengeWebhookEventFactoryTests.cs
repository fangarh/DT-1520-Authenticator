using System.Text.Json;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class ChallengeWebhookEventFactoryTests
{
    [Theory]
    [InlineData(ChallengeStatus.Approved, WebhookEventTypeNames.ChallengeApproved)]
    [InlineData(ChallengeStatus.Denied, WebhookEventTypeNames.ChallengeDenied)]
    [InlineData(ChallengeStatus.Expired, WebhookEventTypeNames.ChallengeExpired)]
    public void CreateFor_ReturnsWebhookPublication_ForTerminalChallengeStates(
        ChallengeStatus status,
        string expectedEventType)
    {
        var challenge = CreateChallenge(status);

        var publication = ChallengeWebhookEventFactory.CreateFor(challenge, DateTimeOffset.Parse("2026-04-20T13:00:00Z"));

        Assert.NotNull(publication);
        Assert.Equal(expectedEventType, publication!.EventType);
        Assert.Equal(WebhookResourceTypeNames.Challenge, publication.ResourceType);
        Assert.Equal(challenge.Id, publication.ResourceId);

        using var document = JsonDocument.Parse(publication.PayloadJson);
        var root = document.RootElement;
        Assert.Equal(expectedEventType, root.GetProperty("eventType").GetString());
        Assert.Equal(challenge.Id, root.GetProperty("challenge").GetProperty("id").GetGuid());
        Assert.Equal(challenge.CorrelationId, root.GetProperty("challenge").GetProperty("correlationId").GetString());
    }

    [Fact]
    public void CreateFor_ReturnsNull_WhenChallengeStateIsNotTerminal()
    {
        var challenge = CreateChallenge(ChallengeStatus.Pending);

        var publication = ChallengeWebhookEventFactory.CreateFor(challenge, DateTimeOffset.UtcNow);

        Assert.Null(publication);
    }

    private static Challenge CreateChallenge(ChallengeStatus status)
    {
        return new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-webhook",
            Username = "user.webhook",
            OperationType = OperationType.Login,
            OperationDisplayName = "Approve login",
            FactorType = FactorType.Push,
            Status = status,
            ExpiresAt = DateTimeOffset.Parse("2026-04-20T13:05:00Z"),
            TargetDeviceId = Guid.NewGuid(),
            ApprovedUtc = status == ChallengeStatus.Approved ? DateTimeOffset.Parse("2026-04-20T13:00:00Z") : null,
            DeniedUtc = status == ChallengeStatus.Denied ? DateTimeOffset.Parse("2026-04-20T13:01:00Z") : null,
            CorrelationId = "corr-001",
            CallbackUrl = new Uri("https://crm.example.com/webhooks/challenges"),
        };
    }
}
