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
    public void ValidateRejectsCallbackUrlsThatDt1520CreateChallengeRejects(string callbackUrl)
    {
        var options = new ReferenceBackendOptions
        {
            TenantId = Guid.Parse("310db3d2-3a5c-4057-b1b2-3caa2ddc204e"),
            ApplicationClientId = Guid.Parse("398bc558-eeff-4719-9d19-b12d72e0f6fe"),
            CallbackUrl = new Uri(callbackUrl),
        };

        var failures = options.Validate();

        Assert.NotEmpty(failures);
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
        Assert.Empty(readiness.ConfigurationIssues);
    }

    private static ReferenceBackendOptions ValidOptions()
    {
        return new ReferenceBackendOptions
        {
            TenantId = Guid.Parse("310db3d2-3a5c-4057-b1b2-3caa2ddc204e"),
            ApplicationClientId = Guid.Parse("398bc558-eeff-4719-9d19-b12d72e0f6fe"),
            CallbackUrl = new Uri("https://reference.example.test/api/reference/callbacks/dt1520"),
        };
    }
}
