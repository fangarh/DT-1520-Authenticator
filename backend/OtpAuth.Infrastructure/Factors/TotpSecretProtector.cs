using System.Security.Cryptography;

namespace OtpAuth.Infrastructure.Factors;

public sealed class TotpSecretProtector
{
    private const int RequiredKeyLength = 32;
    private const int NonceLength = 12;
    private const int TagLength = 16;

    private readonly IReadOnlyDictionary<int, byte[]> _keysByVersion;
    private readonly byte[] _currentKey;
    private readonly int _keyVersion;

    public TotpSecretProtector(TotpProtectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.CurrentKeyVersion <= 0)
        {
            throw new InvalidOperationException("TotpProtection:CurrentKeyVersion must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.CurrentKey))
        {
            throw new InvalidOperationException("TotpProtection:CurrentKey must be configured.");
        }

        var keysByVersion = new Dictionary<int, byte[]>();
        var currentKey = DecodeKey(options.CurrentKey, "TotpProtection:CurrentKey");
        keysByVersion.Add(options.CurrentKeyVersion, currentKey);

        foreach (var additionalKey in options.AdditionalKeys)
        {
            if (additionalKey.KeyVersion <= 0)
            {
                throw new InvalidOperationException("TotpProtection:AdditionalKeys key version must be greater than zero.");
            }

            if (keysByVersion.ContainsKey(additionalKey.KeyVersion))
            {
                throw new InvalidOperationException(
                    $"TOTP protection key version '{additionalKey.KeyVersion}' is configured more than once.");
            }

            keysByVersion.Add(
                additionalKey.KeyVersion,
                DecodeKey(additionalKey.Key, $"TotpProtection:AdditionalKeys[{additionalKey.KeyVersion}]"));
        }

        _currentKey = currentKey;
        _keysByVersion = keysByVersion;
        _keyVersion = options.CurrentKeyVersion;
    }

    public int CurrentKeyVersion => _keyVersion;

    public TotpProtectedSecret Protect(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[secret.Length];
        var tag = new byte[TagLength];

        using var aesGcm = new AesGcm(_currentKey, TagLength);
        aesGcm.Encrypt(nonce, secret, ciphertext, tag);

        return new TotpProtectedSecret
        {
            Ciphertext = ciphertext,
            Nonce = nonce,
            Tag = tag,
            KeyVersion = _keyVersion,
        };
    }

    public byte[] Unprotect(TotpProtectedSecret protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);

        if (!_keysByVersion.TryGetValue(protectedSecret.KeyVersion, out var key))
        {
            throw new InvalidOperationException(
                $"Key version '{protectedSecret.KeyVersion}' is not available in the current TOTP protector.");
        }

        var plaintext = new byte[protectedSecret.Ciphertext.Length];
        using var aesGcm = new AesGcm(key, TagLength);
        aesGcm.Decrypt(protectedSecret.Nonce, protectedSecret.Ciphertext, protectedSecret.Tag, plaintext);

        return plaintext;
    }

    private static byte[] DecodeKey(string? encodedKey, string optionName)
    {
        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            throw new InvalidOperationException($"{optionName} must be configured.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"{optionName} must be a valid base64 string.", exception);
        }

        if (key.Length != RequiredKeyLength)
        {
            throw new InvalidOperationException($"{optionName} must decode to exactly 32 bytes.");
        }

        return key;
    }
}
