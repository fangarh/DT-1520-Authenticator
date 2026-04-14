using OtpAuth.Domain.Challenges;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class ChallengeDataMapperTests
{
    [Fact]
    public void ToPersistenceModel_MapsUriToString()
    {
        var challenge = CreateChallenge();

        var persistenceModel = ChallengeDataMapper.ToPersistenceModel(challenge);

        Assert.Equal(challenge.Id, persistenceModel.Id);
        Assert.Equal(challenge.TenantId, persistenceModel.TenantId);
        Assert.Equal(challenge.ApplicationClientId, persistenceModel.ApplicationClientId);
        Assert.Equal(challenge.CallbackUrl!.ToString(), persistenceModel.CallbackUrl);
        Assert.Equal(challenge.Status, persistenceModel.Status);
        Assert.Equal(challenge.FactorType, persistenceModel.FactorType);
    }

    [Fact]
    public void ToDomainModel_MapsStringToAbsoluteUri()
    {
        var persistenceModel = new ChallengePersistenceModel
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-111",
            Username = "user.name",
            OperationType = OperationType.Login,
            OperationDisplayName = "Sign in",
            FactorType = FactorType.Totp,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CorrelationId = "corr-111",
            CallbackUrl = "https://crm.example.com/hooks/otp",
        };

        var challenge = ChallengeDataMapper.ToDomainModel(persistenceModel);

        Assert.Equal(persistenceModel.Id, challenge.Id);
        Assert.NotNull(challenge.CallbackUrl);
        Assert.Equal(persistenceModel.CallbackUrl, challenge.CallbackUrl!.ToString());
    }

    [Fact]
    public void ToDomainModel_KeepsCallbackUrlNull_WhenSourceIsNull()
    {
        var persistenceModel = new ChallengePersistenceModel
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-222",
            Username = null,
            OperationType = OperationType.StepUp,
            OperationDisplayName = null,
            FactorType = FactorType.Totp,
            Status = ChallengeStatus.Approved,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
            CorrelationId = "corr-222",
            CallbackUrl = null,
        };

        var challenge = ChallengeDataMapper.ToDomainModel(persistenceModel);

        Assert.Null(challenge.CallbackUrl);
    }

    private static Challenge CreateChallenge()
    {
        return new Challenge
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-123",
            Username = "ivan.petrov",
            OperationType = OperationType.Login,
            OperationDisplayName = "Sign in to CRM",
            FactorType = FactorType.Totp,
            Status = ChallengeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CorrelationId = "auth-req-2026-04-14-100",
            CallbackUrl = new Uri("https://crm.example.com/webhooks/otpauth"),
        };
    }
}
