using OtpAuth.Infrastructure.Factors;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class TotpSecretProtectorTests
{
    private static readonly byte[] KeyBytes = "0123456789ABCDEF0123456789ABCDEF"u8.ToArray();

    [Fact]
    public void ProtectAndUnprotect_RoundTripsSecret()
    {
        var protector = CreateProtector();
        var secret = "ABCDEFGHIJKLMNOPQRSTUVWX12345678"u8.ToArray();

        var protectedSecret = protector.Protect(secret);
        var plaintext = protector.Unprotect(protectedSecret);

        Assert.Equal(secret, plaintext);
        Assert.Equal(1, protectedSecret.KeyVersion);
    }

    [Fact]
    public void Constructor_Throws_WhenKeyIsMissing()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new TotpSecretProtector(new TotpProtectionOptions()));

        Assert.Contains("CurrentKey", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_Throws_WhenKeyVersionDoesNotMatch()
    {
        var protector = CreateProtector();

        var error = Assert.Throws<InvalidOperationException>(() =>
            protector.Unprotect(new TotpProtectedSecret
            {
                Ciphertext = [1, 2, 3],
                Nonce = new byte[12],
                Tag = new byte[16],
                KeyVersion = 99,
            }));

        Assert.Contains("Key version", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unprotect_UsesAdditionalLegacyKey_WhenStoredVersionIsNotCurrent()
    {
        var legacyKey = "ABCDEFGHIJKLMNOPQRSTUVWX12345678"u8.ToArray();
        var legacyProtector = new TotpSecretProtector(new TotpProtectionOptions
        {
            CurrentKey = Convert.ToBase64String(legacyKey),
            CurrentKeyVersion = 1,
        });
        var secret = "ZYXWVUTSRQPONMLKJIHGFEDCBA123456"u8.ToArray();
        var protectedByLegacyKey = legacyProtector.Protect(secret);

        var protector = new TotpSecretProtector(new TotpProtectionOptions
        {
            CurrentKey = Convert.ToBase64String(KeyBytes),
            CurrentKeyVersion = 2,
            AdditionalKeys =
            [
                new TotpProtectionKeyOptions
                {
                    KeyVersion = 1,
                    Key = Convert.ToBase64String(legacyKey),
                },
            ],
        });

        var plaintext = protector.Unprotect(protectedByLegacyKey);

        Assert.Equal(secret, plaintext);
    }

    [Fact]
    public void Constructor_Throws_WhenKeyVersionIsDuplicated()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new TotpSecretProtector(new TotpProtectionOptions
            {
                CurrentKey = Convert.ToBase64String(KeyBytes),
                CurrentKeyVersion = 1,
                AdditionalKeys =
                [
                    new TotpProtectionKeyOptions
                    {
                        KeyVersion = 1,
                        Key = Convert.ToBase64String(KeyBytes),
                    },
                ],
            }));

        Assert.Contains("configured more than once", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TotpSecretProtector CreateProtector()
    {
        return new TotpSecretProtector(new TotpProtectionOptions
        {
            CurrentKey = Convert.ToBase64String(KeyBytes),
            CurrentKeyVersion = 1,
        });
    }
}
