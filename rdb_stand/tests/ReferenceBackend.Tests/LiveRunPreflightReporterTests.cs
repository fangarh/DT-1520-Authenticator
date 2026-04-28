using Dt1520.Authenticator.ReferenceBackend;
using Microsoft.Extensions.Configuration;

namespace Dt1520.Authenticator.ReferenceBackend.Tests;

public sealed class LiveRunPreflightReporterTests
{
    [Fact]
    public void CreateReportsMissingConfigurationWithoutSecretValues()
    {
        var configuration = new ConfigurationBuilder().Build();

        var report = LiveRunPreflightReporter.Create(configuration);

        Assert.False(report.IsReadyForBackendStart);
        Assert.False(report.HasClientSecret);
        Assert.Contains(report.ConfigurationIssues, issue => issue.Contains("ClientSecret", StringComparison.Ordinal));
        Assert.DoesNotContain(report.ConfigurationIssues, issue => issue.Contains("secret-one", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateReportsReadyConfigurationWithoutEchoingSecrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dt1520Authenticator:BaseUrl"] = "https://admin.ghostring.ru:18443/",
                ["Dt1520Authenticator:ClientId"] = "client-one",
                ["Dt1520Authenticator:ClientSecret"] = "secret-one",
                ["Dt1520Authenticator:CallbackSigningSecret"] = "callback-secret",
                ["ReferenceBackend:TenantId"] = "310db3d2-3a5c-4057-b1b2-3caa2ddc204e",
                ["ReferenceBackend:ApplicationClientId"] = "398bc558-eeff-4719-9d19-b12d72e0f6fe",
                ["ReferenceBackend:CallbackUrl"] = "https://reference.example.test/api/reference/callbacks/dt1520",
            })
            .Build();

        var report = LiveRunPreflightReporter.Create(configuration);
        var serialized = System.Text.Json.JsonSerializer.Serialize(report);

        Assert.True(report.IsReadyForBackendStart);
        Assert.Equal("admin.ghostring.ru", report.Dt1520BaseUrlHost);
        Assert.Equal("reference.example.test", report.CallbackUrlHost);
        Assert.Equal("PublicInternet", report.CallbackUrlPolicyMode);
        Assert.False(report.AllowInsecureCallbackHttp);
        Assert.DoesNotContain("secret-one", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("callback-secret", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandReturnsNonZeroWhenConfigurationIsMissing()
    {
        using var output = new StringWriter();

        var exitCode = LiveRunPreflightCommand.Run(new ConfigurationBuilder().Build(), output);

        Assert.Equal(2, exitCode);
        Assert.Contains("\"isReadyForBackendStart\": false", output.ToString(), StringComparison.Ordinal);
    }
}
