using System.Text;
using OtpAuth.Infrastructure.Challenges;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Challenges;

public sealed class PushChallengeDeliveryGatewayOptionsTests
{
    [Fact]
    public void Validate_RejectsUnknownProvider()
    {
        var options = new PushChallengeDeliveryGatewayOptions
        {
            Provider = "unknown-provider",
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Equal(
            "PushDelivery:Provider must be one of 'logging' or 'fcm'.",
            exception.Message);
    }

    [Fact]
    public void Validate_RejectsFcmCredentialsThatAreNotServiceAccounts()
    {
        var options = new PushChallengeDeliveryGatewayOptions
        {
            Provider = PushChallengeDeliveryProviderNames.Fcm,
            Fcm = new FcmPushChallengeDeliveryGatewayOptions
            {
                ProjectId = "test-project",
                ServiceAccountJsonBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    """
                    {
                      "type": "authorized_user",
                      "client_email": "svc@test-project.iam.gserviceaccount.com",
                      "private_key": "key"
                    }
                    """)),
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Equal(
            "PushDelivery:Fcm credentials must be a Google service_account JSON document.",
            exception.Message);
    }
}
