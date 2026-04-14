using OtpAuth.Infrastructure.Factors;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class PostgresTotpVerificationRateLimiterTests
{
    [Theory]
    [InlineData(0, true, null)]
    [InlineData(4, true, null)]
    [InlineData(5, false, 600)]
    [InlineData(8, false, 600)]
    public void CreateDecision_AppliesConfiguredThreshold(
        int attempts,
        bool expectedAllowed,
        int? expectedRetryAfterSeconds)
    {
        var decision = PostgresTotpVerificationRateLimiter.CreateDecision(attempts);

        Assert.Equal(expectedAllowed, decision.IsAllowed);
        Assert.Equal(expectedRetryAfterSeconds, decision.RetryAfterSeconds);
    }
}
