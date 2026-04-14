using OtpAuth.Application.Policy;
using OtpAuth.Domain.Policy;
using OtpAuth.Infrastructure.Policy;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Policy;

public sealed class DefaultPolicyEvaluatorTests
{
    private readonly DefaultPolicyEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_DeniesIncompleteContext()
    {
        var context = CreateValidContext() with
        {
            TenantId = Guid.Empty,
        };

        var decision = _evaluator.Evaluate(context);

        Assert.True(decision.IsDenied);
        Assert.Equal("Policy context is incomplete.", decision.DenyReason);
        Assert.Empty(decision.AllowedFactors);
    }

    [Fact]
    public void Evaluate_AllowsPushForLogin_WhenDeviceActiveAndPushChannelAvailable()
    {
        var context = CreateValidContext();

        var decision = _evaluator.Evaluate(context);

        Assert.False(decision.IsDenied);
        Assert.True(decision.RequiresSecondFactor);
        Assert.True(decision.PushAllowed);
        Assert.Equal(FactorType.Push, decision.PreferredFactor);
        Assert.Contains(FactorType.Push, decision.AllowedFactors);
    }

    [Fact]
    public void Evaluate_FallsBackToTotp_WhenPushChannelIsUnavailable()
    {
        var context = CreateValidContext() with
        {
            PushChannelAvailable = false,
        };

        var decision = _evaluator.Evaluate(context);

        Assert.False(decision.IsDenied);
        Assert.False(decision.PushAllowed);
        Assert.True(decision.TotpAllowed);
        Assert.Equal(FactorType.Totp, decision.PreferredFactor);
        Assert.DoesNotContain(FactorType.Push, decision.AllowedFactors);
    }

    [Fact]
    public void Evaluate_DeniesSelfServiceTotpEnrollment()
    {
        var context = CreateValidContext() with
        {
            OperationType = OperationType.TotpEnrollment,
            ChallengePurpose = ChallengePurpose.Enrollment,
            RequestedFactor = FactorType.Totp,
            EnrollmentInitiationSource = EnrollmentInitiationSource.SelfService,
            DeviceTrustState = DeviceTrustState.None,
            AvailableFactors = [FactorType.Totp],
        };

        var decision = _evaluator.Evaluate(context);

        Assert.True(decision.IsDenied);
        Assert.False(decision.EnrollmentAllowed);
        Assert.Equal("Enrollment initiation source is not trusted.", decision.DenyReason);
    }

    [Fact]
    public void Evaluate_DeniesRequestedPush_WhenDeviceIsRevoked()
    {
        var context = CreateValidContext() with
        {
            RequestedFactor = FactorType.Push,
            DeviceTrustState = DeviceTrustState.Revoked,
        };

        var decision = _evaluator.Evaluate(context);

        Assert.True(decision.IsDenied);
        Assert.False(decision.PushAllowed);
        Assert.True(decision.TotpAllowed);
        Assert.Equal("Requested factor 'Push' is not allowed.", decision.DenyReason);
        Assert.DoesNotContain(FactorType.Push, decision.AllowedFactors);
    }

    private static PolicyContext CreateValidContext()
    {
        return new PolicyContext
        {
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            OperationType = OperationType.Login,
            UserId = Guid.NewGuid(),
            UserStatus = UserStatus.Active,
            RequestedFactor = null,
            AvailableFactors = [FactorType.Push, FactorType.Totp, FactorType.BackupCode],
            DeviceTrustState = DeviceTrustState.Active,
            DeploymentProfile = DeploymentProfile.Cloud,
            EnvironmentMode = EnvironmentMode.Production,
            ChallengePurpose = ChallengePurpose.Authentication,
            EnrollmentInitiationSource = EnrollmentInitiationSource.Admin,
            PushChannelAvailable = true,
        };
    }
}
