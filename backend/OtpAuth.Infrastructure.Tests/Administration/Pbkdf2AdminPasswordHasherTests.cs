using OtpAuth.Infrastructure.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class Pbkdf2AdminPasswordHasherTests
{
    [Fact]
    public void Hash_AndVerify_ReturnTrue_ForMatchingPassword()
    {
        var hasher = new Pbkdf2AdminPasswordHasher();

        var hash = hasher.Hash("correct horse battery staple");

        Assert.True(hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hasher = new Pbkdf2AdminPasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.False(hasher.Verify("wrong password", hash));
    }
}
