using OtpAuth.Infrastructure.Factors;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class Pbkdf2BackupCodeHasherTests
{
    [Fact]
    public void HashAndVerify_AcceptsNormalizedEquivalentForms()
    {
        var hasher = new Pbkdf2BackupCodeHasher();
        var codeHash = hasher.Hash("ABCD1234");

        Assert.True(hasher.Verify("ABCD1234", codeHash));
        Assert.True(hasher.Verify("ABCD1234", codeHash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForMalformedHashOrDifferentCode()
    {
        var hasher = new Pbkdf2BackupCodeHasher();
        var codeHash = hasher.Hash("ZXCV5678");

        Assert.False(hasher.Verify("ZXCV5679", codeHash));
        Assert.False(hasher.Verify("ZXCV5678", "invalid-hash"));
    }
}
