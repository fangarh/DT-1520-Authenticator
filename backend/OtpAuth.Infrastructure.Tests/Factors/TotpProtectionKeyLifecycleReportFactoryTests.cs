using OtpAuth.Infrastructure.Factors;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class TotpProtectionKeyLifecycleReportFactoryTests
{
    [Fact]
    public void Create_BuildsWarningsForBacklogAndUnconfiguredUsage()
    {
        var report = new TotpProtectionKeyLifecycleReportFactory().Create(
            new TotpProtectionOptions
            {
                CurrentKeyVersion = 2,
                AdditionalKeys =
                [
                    new TotpProtectionKeyOptions
                    {
                        KeyVersion = 1,
                        Key = "ignored-for-report",
                    },
                    new TotpProtectionKeyOptions
                    {
                        KeyVersion = 3,
                        Key = "ignored-for-report",
                    },
                ],
            },
            [
                new TotpEnrollmentKeyVersionUsage
                {
                    KeyVersion = 1,
                    EnrollmentCount = 4,
                },
                new TotpEnrollmentKeyVersionUsage
                {
                    KeyVersion = 4,
                    EnrollmentCount = 2,
                },
            ],
            DateTimeOffset.UtcNow);

        Assert.Equal(6, report.EnrollmentsRequiringReEncryptionCount);
        Assert.Contains(report.Warnings, warning => warning.Contains("still require re-encryption", StringComparison.Ordinal));
        Assert.Contains(report.Warnings, warning => warning.Contains("not configured in runtime", StringComparison.Ordinal));
        Assert.Contains(report.Warnings, warning => warning.Contains("configured without remaining enrollments", StringComparison.Ordinal));
    }
}
