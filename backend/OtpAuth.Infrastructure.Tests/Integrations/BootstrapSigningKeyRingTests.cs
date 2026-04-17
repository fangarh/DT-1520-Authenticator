using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Integrations;

public sealed class BootstrapSigningKeyRingTests
{
    [Fact]
    public void Constructor_ExcludesLegacyKey_WhenRetirementTimeHasPassed()
    {
        var keyRing = new BootstrapSigningKeyRing(new BootstrapOAuthOptions
        {
            CurrentSigningKeyId = "key-v2",
            CurrentSigningKey = "integration-tests-signing-key-0987654321",
            AdditionalSigningKeys =
            [
                new BootstrapOAuthSigningKeyOptions
                {
                    KeyId = "key-v1",
                    Key = "integration-tests-signing-key-1234567890",
                    RetireAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                },
            ],
        });

        var descriptor = Assert.Single(keyRing.Descriptors, value => value.KeyId == "key-v1");

        Assert.False(descriptor.IsAcceptedForValidation);
        Assert.Empty(keyRing.ResolveValidationKeys("key-v1"));
    }

    [Fact]
    public void Constructor_Throws_WhenSigningKeyIsTooShort()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new BootstrapSigningKeyRing(new BootstrapOAuthOptions
        {
            CurrentSigningKeyId = "key-v1",
            CurrentSigningKey = "short-signing-key",
        }));

        Assert.Equal("BootstrapOAuth signing key 'key-v1' must be at least 32 bytes.", exception.Message);
    }
}
