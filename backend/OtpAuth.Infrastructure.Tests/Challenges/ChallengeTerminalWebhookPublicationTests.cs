using OtpAuth.Application.Challenges;
using OtpAuth.Application.Factors;
using OtpAuth.Application.Integrations;
using OtpAuth.Application.Webhooks;
using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class ChallengeTerminalWebhookPublicationTests
{
    [Fact]
    public async Task VerifyTotpHandler_EnqueuesTopLevelWebhook_ForMatchingSubscription()
    {
        var repository = new InMemoryChallengeRepository();
        var challenge = CreateChallenge();
        repository.SeedWebhookSubscription(new WebhookSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            TenantId = challenge.TenantId,
            ApplicationClientId = challenge.ApplicationClientId,
            EndpointUrl = new Uri("https://crm.example.com/webhooks/platform"),
            IsActive = true,
            EventTypes = [WebhookEventTypeNames.ChallengeApproved],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await repository.AddAsync(challenge, CancellationToken.None);
        var handler = new VerifyTotpHandler(
            repository,
            new RecordingChallengeAttemptRecorder(),
            new StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision.Allowed()),
            new StubTotpVerifier(TotpVerificationResult.Valid(Guid.NewGuid(), 123456L)));

        var result = await handler.HandleAsync(
            new VerifyTotpRequest
            {
                ChallengeId = challenge.Id,
                Code = "123456",
            },
            new IntegrationClientContext
            {
                ClientId = "client-webhook",
                TenantId = challenge.TenantId,
                ApplicationClientId = challenge.ApplicationClientId,
                Scopes = [IntegrationClientScopes.ChallengesWrite],
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var delivery = Assert.Single(repository.GetWebhookDeliveries());
        Assert.Equal(WebhookEventTypeNames.ChallengeApproved, delivery.EventType);
        Assert.Equal(WebhookResourceTypeNames.Challenge, delivery.ResourceType);
        Assert.Equal("https://crm.example.com/webhooks/platform", delivery.EndpointUrl.ToString());
        Assert.Contains("\"eventType\":\"challenge.approved\"", delivery.PayloadJson);
        Assert.Contains(challenge.Id.ToString(), delivery.PayloadJson);
    }

    private static Challenge CreateChallenge()
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
            FactorType = FactorType.Totp,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CorrelationId = "corr-001",
            CallbackUrl = new Uri("https://crm.example.com/webhooks/challenges"),
        };
    }

    private sealed class StubTotpVerificationRateLimiter : ITotpVerificationRateLimiter
    {
        private readonly TotpVerificationRateLimitDecision _decision;

        public StubTotpVerificationRateLimiter(TotpVerificationRateLimitDecision decision)
        {
            _decision = decision;
        }

        public Task<TotpVerificationRateLimitDecision> EvaluateAsync(
            Challenge challenge,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_decision);
        }
    }

    private sealed class StubTotpVerifier : ITotpVerifier
    {
        private readonly TotpVerificationResult _result;

        public StubTotpVerifier(TotpVerificationResult result)
        {
            _result = result;
        }

        public Task<TotpVerificationResult> VerifyAsync(
            Challenge challenge,
            string code,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class RecordingChallengeAttemptRecorder : IChallengeAttemptRecorder
    {
        public Task RecordAsync(ChallengeAttemptRecord attempt, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
