using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class IntegrationClientLifecycleAuditFactoryTests
{
    [Fact]
    public void CreateSecretRotatedEntry_OnlyPersistsMetadata()
    {
        var factory = new IntegrationClientLifecycleAuditFactory();

        var entry = factory.CreateSecretRotatedEntry("crm-client", DateTimeOffset.Parse("2026-04-15T10:15:00Z"), explicitSecretProvided: true);

        Assert.Equal("integration_client_lifecycle.secret_rotated", entry.EventType);
        Assert.Equal("integration_client", entry.SubjectType);
        Assert.Equal("crm-client", entry.SubjectId);
        Assert.Contains("\"explicitSecretProvided\":true", entry.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("newClientSecret", entry.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("clientSecretHash", entry.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateStateChangedEntry_UsesWarningSeverity_WhenStateWasAlreadyApplied()
    {
        var factory = new IntegrationClientLifecycleAuditFactory();

        var entry = factory.CreateStateChangedEntry(
            "crm-client",
            isActive: false,
            stateChanged: false,
            changedAtUtc: DateTimeOffset.Parse("2026-04-15T10:15:00Z"));

        Assert.Equal("integration_client_lifecycle.state_change_skipped", entry.EventType);
        Assert.Equal("warning", entry.Severity);
        Assert.Contains("\"outcome\":\"already_applied\"", entry.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("clientSecret", entry.PayloadJson, StringComparison.Ordinal);
    }
}
