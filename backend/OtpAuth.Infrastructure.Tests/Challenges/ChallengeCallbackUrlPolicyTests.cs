using OtpAuth.Application.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class ChallengeCallbackUrlPolicyTests
{
    [Fact]
    public void PublicInternetPolicy_AllowsExternalHttpsCallback()
    {
        var policy = ChallengeCallbackUrlPolicy.PublicInternet;

        var result = policy.Validate(new Uri("https://crm.example.test/hooks/otpauth"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("http://crm.example.test/hooks/otpauth")]
    [InlineData("https://localhost/hooks/otpauth")]
    [InlineData("https://127.0.0.1/hooks/otpauth")]
    [InlineData("https://10.0.0.5/hooks/otpauth")]
    [InlineData("https://operator:secret@crm.example.test/hooks/otpauth")]
    [InlineData("https://crm.example.test/")]
    [InlineData("https://crm.example.test/hooks/otpauth#token")]
    public void PublicInternetPolicy_RejectsUnsafeCallbackUrls(string callbackUrl)
    {
        var policy = ChallengeCallbackUrlPolicy.PublicInternet;

        var result = policy.Validate(new Uri(callbackUrl));

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("PublicInternet policy", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(callbackUrl, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrivateNetworkPolicy_AllowsPrivateIpHttpsCallback()
    {
        var policy = new ChallengeCallbackUrlPolicy(ChallengeCallbackUrlPolicyMode.PrivateNetwork);

        var result = policy.Validate(new Uri("https://10.0.0.5/hooks/otpauth"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void PrivateNetworkPolicy_RejectsHttpUnlessExplicitlyAllowed()
    {
        var policy = new ChallengeCallbackUrlPolicy(ChallengeCallbackUrlPolicyMode.PrivateNetwork);

        var result = policy.Validate(new Uri("http://internal-auth/hooks/otpauth"));

        Assert.False(result.IsValid);
        Assert.Contains("PrivateNetwork policy", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("HTTPS is required", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PrivateNetworkPolicy_AllowsHttpPrivateDns_WhenExplicitlyConfigured()
    {
        var policy = new ChallengeCallbackUrlPolicy(
            ChallengeCallbackUrlPolicyMode.PrivateNetwork,
            allowInsecureHttp: true);

        var result = policy.Validate(new Uri("http://internal-auth/hooks/otpauth"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("http://localhost/api/reference/callbacks/dt1520")]
    [InlineData("http://127.0.0.1:5188/api/reference/callbacks/dt1520")]
    public void LocalDevelopmentPolicy_AllowsLocalHttpCallback(string callbackUrl)
    {
        var policy = new ChallengeCallbackUrlPolicy(ChallengeCallbackUrlPolicyMode.LocalDevelopment);

        var result = policy.Validate(new Uri(callbackUrl));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void FromOptions_RejectsUnknownMode()
    {
        var options = new ChallengeCallbackUrlPolicyOptions
        {
            Mode = "UnsafeMode",
        };

        var exception = Assert.Throws<InvalidOperationException>(() => ChallengeCallbackUrlPolicy.FromOptions(options));

        Assert.Contains("ChallengeCallbackUrlPolicy:Mode", exception.Message, StringComparison.Ordinal);
    }
}
