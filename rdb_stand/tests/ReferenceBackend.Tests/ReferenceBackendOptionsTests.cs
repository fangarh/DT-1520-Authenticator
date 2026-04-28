using Dt1520.Authenticator.ReferenceBackend;
using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.ReferenceBackend.Tests;

public sealed class ReferenceBackendOptionsTests
{
    [Fact]
    public void ValidateAcceptsExternalHttpsCallbackUrl()
    {
        var options = ValidOptions();

        Assert.Empty(options.Validate());
    }

    [Theory]
    [InlineData("http://reference.example.test/api/reference/callbacks/dt1520")]
    [InlineData("https://localhost/api/reference/callbacks/dt1520")]
    [InlineData("https://127.0.0.1/api/reference/callbacks/dt1520")]
    [InlineData("https://192.168.1.15/api/reference/callbacks/dt1520")]
    [InlineData("https://operator:secret@reference.example.test/api/reference/callbacks/dt1520")]
    public void ValidateRejectsUnsafeCallbackUrlsUnderPublicInternetPolicy(string callbackUrl)
    {
        var options = new ReferenceBackendOptions
        {
            TenantId = Guid.Parse("310db3d2-3a5c-4057-b1b2-3caa2ddc204e"),
            ApplicationClientId = Guid.Parse("398bc558-eeff-4719-9d19-b12d72e0f6fe"),
            CallbackUrl = new Uri(callbackUrl),
        };

        var failures = options.Validate();

        Assert.NotEmpty(failures);
        Assert.Contains(failures, failure => failure.Contains("PublicInternet policy", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains(callbackUrl, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAcceptsPrivateNetworkCallback_WhenPolicyIsExplicit()
    {
        var options = ValidOptions(
            callbackUrl: "https://192.168.1.15/api/reference/callbacks/dt1520",
            callbackUrlPolicyMode: "PrivateNetwork");

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void ValidateAcceptsLocalHttpCallback_WhenLocalDevelopmentPolicyIsExplicit()
    {
        var options = ValidOptions(
            callbackUrl: "http://localhost:5188/api/reference/callbacks/dt1520",
            callbackUrlPolicyMode: "LocalDevelopment");

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void ValidateRejectsUnknownCallbackPolicyMode()
    {
        var options = ValidOptions(callbackUrlPolicyMode: "UnsafeMode");

        var failures = options.Validate();

        Assert.Contains(
            "CallbackUrlPolicyMode must be one of: PublicInternet, PrivateNetwork, LocalDevelopment.",
            failures);
    }

    [Fact]
    public void ReadinessReporterReturnsSanitizedConfigurationStatus()
    {
        var reporter = new ReferenceBackendReadinessReporter(Options.Create(ValidOptions()));

        var readiness = reporter.GetReadiness();

        Assert.True(readiness.IsReadyForLiveRun);
        Assert.True(readiness.HasTenantId);
        Assert.True(readiness.HasApplicationClientId);
        Assert.Equal(new Uri("https://reference.example.test/api/reference/callbacks/dt1520"), readiness.CallbackUrl);
        Assert.Equal("PublicInternet", readiness.CallbackUrlPolicyMode);
        Assert.False(readiness.AllowInsecureCallbackHttp);
        Assert.Empty(readiness.ConfigurationIssues);
    }

    private static ReferenceBackendOptions ValidOptions(
        string callbackUrl = "https://reference.example.test/api/reference/callbacks/dt1520",
        string callbackUrlPolicyMode = "PublicInternet",
        bool allowInsecureCallbackHttp = false)
    {
        return new ReferenceBackendOptions
        {
            TenantId = Guid.Parse("310db3d2-3a5c-4057-b1b2-3caa2ddc204e"),
            ApplicationClientId = Guid.Parse("398bc558-eeff-4719-9d19-b12d72e0f6fe"),
            CallbackUrl = new Uri(callbackUrl),
            CallbackUrlPolicyMode = callbackUrlPolicyMode,
            AllowInsecureCallbackHttp = allowInsecureCallbackHttp,
        };
    }
}
