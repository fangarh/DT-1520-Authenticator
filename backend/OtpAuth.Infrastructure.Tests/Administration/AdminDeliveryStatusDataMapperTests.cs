using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminDeliveryStatusDataMapperTests
{
    [Fact]
    public void ToDomainModel_StripsSecretBearingDestinationParts_ForChallengeCallbacks()
    {
        var model = new AdminDeliveryStatusPersistenceModel
        {
            DeliveryId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            Channel = "challenge_callback",
            Status = "queued",
            EventType = "challenge.approved",
            DestinationUrl = "https://user:secret@crm.example.com/hooks/challenge?sig=top-secret#fragment",
            SubjectType = "challenge",
            SubjectId = Guid.NewGuid(),
            AttemptCount = 1,
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
            NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(2),
            LastAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            LastErrorCode = "timeout",
        };

        var view = AdminDeliveryStatusDataMapper.ToDomainModel(model);

        Assert.Equal(AdminDeliveryChannel.ChallengeCallback, view.Channel);
        Assert.Equal(AdminDeliveryStatus.Queued, view.Status);
        Assert.Equal("https://crm.example.com/hooks/challenge", view.DeliveryDestination);
        Assert.True(view.IsRetryScheduled);
    }

    [Fact]
    public void ToDomainModel_MapsWebhookPublicationMetadata()
    {
        var publicationId = Guid.NewGuid();
        var model = new AdminDeliveryStatusPersistenceModel
        {
            DeliveryId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            Channel = "webhook_event",
            Status = "delivered",
            EventType = "device.activated",
            DestinationUrl = "https://crm.example.com:8443/hooks/platform?token=secret",
            SubjectType = "device",
            SubjectId = Guid.NewGuid(),
            PublicationId = publicationId,
            AttemptCount = 1,
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-4),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-4),
            NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-4),
            DeliveredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
        };

        var view = AdminDeliveryStatusDataMapper.ToDomainModel(model);

        Assert.Equal(AdminDeliveryChannel.WebhookEvent, view.Channel);
        Assert.Equal(AdminDeliveryStatus.Delivered, view.Status);
        Assert.Equal("https://crm.example.com:8443/hooks/platform", view.DeliveryDestination);
        Assert.Equal(publicationId, view.PublicationId);
        Assert.Equal("device", view.SubjectType);
    }

    [Fact]
    public void ToDomainModel_Throws_WhenChannelIsUnsupported()
    {
        var model = new AdminDeliveryStatusPersistenceModel
        {
            DeliveryId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationClientId = Guid.NewGuid(),
            Channel = "push_delivery",
            Status = "failed",
            EventType = "device.blocked",
            DestinationUrl = "https://crm.example.com/hooks/platform",
            SubjectType = "device",
            SubjectId = Guid.NewGuid(),
            AttemptCount = 2,
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-8),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-8),
            NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        var exception = Assert.Throws<InvalidOperationException>(() => AdminDeliveryStatusDataMapper.ToDomainModel(model));

        Assert.Equal("Unsupported admin delivery channel 'push_delivery'.", exception.Message);
    }
}
