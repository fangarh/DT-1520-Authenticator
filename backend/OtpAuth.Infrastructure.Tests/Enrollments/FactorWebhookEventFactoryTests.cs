using System.Text.Json;
using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Webhooks;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Enrollments;

public sealed class FactorWebhookEventFactoryTests
{
    [Fact]
    public void CreateForTotp_ReturnsFactorRevokedPublication_WithSanitizedPayload()
    {
        var enrollment = new TotpEnrollmentProvisioningRecord
        {
            EnrollmentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            ExternalUserId = "user-factor",
            Label = "ivan.petrov",
            Secret = [1, 2, 3],
            Digits = 6,
            PeriodSeconds = 30,
            Algorithm = "SHA1",
            IsActive = true,
            ConfirmedUtc = DateTimeOffset.Parse("2026-04-20T14:00:00Z"),
            RevokedUtc = null,
            FailedConfirmationAttempts = 0,
            PendingReplacement = null,
        };

        var publication = FactorRevocationWebhookEventFactory.CreateForTotp(
            enrollment,
            DateTimeOffset.Parse("2026-04-20T14:05:00Z"));

        Assert.Equal(WebhookEventTypeNames.FactorRevoked, publication.EventType);
        Assert.Equal(WebhookResourceTypeNames.Factor, publication.ResourceType);
        Assert.Equal(enrollment.EnrollmentId, publication.ResourceId);

        using var document = JsonDocument.Parse(publication.PayloadJson);
        var root = document.RootElement;
        Assert.Equal("factor.revoked", root.GetProperty("eventType").GetString());
        Assert.Equal("totp", root.GetProperty("factorType").GetString());
        Assert.Equal("user-factor", root.GetProperty("subject").GetProperty("externalUserId").GetString());
        Assert.False(root.TryGetProperty("secret", out _));
        Assert.False(root.TryGetProperty("label", out _));
    }
}
