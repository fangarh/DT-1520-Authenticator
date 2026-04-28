using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.Client.Tests;

public sealed class ClientSecretRedactionTests
{
    [Fact]
    public void SecretBearingTypesDoNotExposeSecretsInToString()
    {
        var credentials = new Dt1520AuthenticatorClientCredentials("client-one", "secret-one");
        var token = new Dt1520AuthenticatorAccessToken(
            "token-one",
            "Bearer",
            new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero),
            "challenges:read");

        Assert.DoesNotContain("secret-one", credentials.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("token-one", token.ToString(), StringComparison.Ordinal);
        Assert.Contains("[redacted]", credentials.ToString(), StringComparison.Ordinal);
        Assert.Contains("[redacted]", token.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OptionsValidationDoesNotEchoClientSecret()
    {
        var options = new Dt1520AuthenticatorClientOptions
        {
            BaseUrl = new Uri("https://auth.test?unexpected=true"),
            Credentials = new Dt1520AuthenticatorClientCredentials("client-one", "secret-one"),
        };

        var exception = Assert.Throws<ArgumentException>(() => options.Validate());

        Assert.DoesNotContain("secret-one", exception.ToString(), StringComparison.Ordinal);
    }
}
