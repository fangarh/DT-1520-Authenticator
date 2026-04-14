using OtpAuth.Infrastructure.Factors;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class BootstrapTotpSecretProviderTests
{
    [Fact]
    public void LoadFromEnvironmentOrRandom_ReturnsDecodedSecret_WhenBase64IsValid()
    {
        var expected = "0123456789ABCDEF0123456789ABCDEF"u8.ToArray();
        var base64 = Convert.ToBase64String(expected);

        var actual = BootstrapTotpSecretProvider.LoadFromEnvironmentOrRandom(base64);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LoadFromEnvironmentOrRandom_Throws_WhenBase64IsInvalid()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            BootstrapTotpSecretProvider.LoadFromEnvironmentOrRandom("not-base64"));

        Assert.Contains("base64", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadFromEnvironmentOrRandom_Throws_WhenSecretIsTooShort()
    {
        var tooShort = Convert.ToBase64String("short-secret"u8.ToArray());

        var error = Assert.Throws<InvalidOperationException>(() =>
            BootstrapTotpSecretProvider.LoadFromEnvironmentOrRandom(tooShort));

        Assert.Contains("at least 16 bytes", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
