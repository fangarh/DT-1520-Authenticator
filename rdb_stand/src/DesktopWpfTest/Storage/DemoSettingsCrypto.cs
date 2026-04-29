using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dt1520.Authenticator.DesktopWpfTest.Models;

namespace Dt1520.Authenticator.DesktopWpfTest.Storage;

public sealed class DemoSettingsCrypto
{
    private const int SaltSizeBytes = 16;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int IterationCount = 150_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _profileSecret;

    public DemoSettingsCrypto(string? profileSecret = null)
    {
        _profileSecret = string.IsNullOrWhiteSpace(profileSecret)
            ? BuildDefaultProfileSecret()
            : profileSecret;
    }

    public EncryptedSettingsEnvelope Encrypt(DemoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var tag = new byte[TagSizeBytes];
        var ciphertext = new byte[plaintext.Length];
        var key = DeriveKey(salt);

        try
        {
            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }

        return new EncryptedSettingsEnvelope
        {
            Version = 1,
            Salt = Convert.ToBase64String(salt),
            Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag),
            Ciphertext = Convert.ToBase64String(ciphertext),
        };
    }

    public DemoSettings Decrypt(EncryptedSettingsEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Version != 1)
        {
            throw new InvalidDataException("Unsupported settings file version.");
        }

        var salt = Convert.FromBase64String(envelope.Salt);
        var nonce = Convert.FromBase64String(envelope.Nonce);
        var tag = Convert.FromBase64String(envelope.Tag);
        var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        var plaintext = new byte[ciphertext.Length];
        var key = DeriveKey(salt);

        try
        {
            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return JsonSerializer.Deserialize<DemoSettings>(plaintext, JsonOptions)
                ?? throw new InvalidDataException("Settings file does not contain a valid JSON object.");
        }
        catch (CryptographicException ex)
        {
            throw new InvalidDataException("Settings file cannot be decrypted for the current local profile.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private byte[] DeriveKey(byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(_profileSecret),
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            KeySizeBytes);
    }

    private static string BuildDefaultProfileSecret()
    {
        return string.Join(
            "|",
            Environment.UserName,
            Environment.UserDomainName,
            Environment.MachineName,
            typeof(DemoSettingsCrypto).Assembly.FullName);
    }
}
