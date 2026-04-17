using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class BootstrapSigningKeyLifecycleReportFactoryTests
{
    [Fact]
    public void Create_IncludesWarningsForIndefiniteAndRetiredLegacyKeys()
    {
        var options = new BootstrapOAuthOptions
        {
            CurrentSigningKeyId = "key-v3",
            CurrentSigningKey = "integration-tests-signing-key-112233445566",
            AccessTokenLifetimeMinutes = 60,
            AdditionalSigningKeys =
            [
                new BootstrapOAuthSigningKeyOptions
                {
                    KeyId = "key-v2",
                    Key = "integration-tests-signing-key-223344556677",
                    RetireAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                },
                new BootstrapOAuthSigningKeyOptions
                {
                    KeyId = "key-v1",
                    Key = "integration-tests-signing-key-334455667788",
                },
                new BootstrapOAuthSigningKeyOptions
                {
                    KeyId = "key-v0",
                    Key = "integration-tests-signing-key-445566778899",
                    RetireAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                },
            ],
        };
        var report = new BootstrapSigningKeyLifecycleReportFactory().Create(
            options,
            new BootstrapSigningKeyRing(options),
            DateTimeOffset.UtcNow);

        Assert.Equal("key-v3", report.CurrentSigningKeyId);
        Assert.Equal(2, report.ActiveLegacyKeyCount);
        Assert.Equal(1, report.RetiredLegacyKeyCount);
        Assert.Contains(report.Warnings, warning => warning.Contains("Legacy keys without RetireAtUtc", StringComparison.Ordinal));
        Assert.Contains(report.Warnings, warning => warning.Contains("Retired legacy keys are still present", StringComparison.Ordinal));
    }
}
