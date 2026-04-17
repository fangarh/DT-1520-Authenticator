using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class BootstrapSigningKeyRing
{
    private readonly IReadOnlyDictionary<string, SecurityKey> _validationKeysById;

    public BootstrapSigningKeyRing(BootstrapOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var now = DateTimeOffset.UtcNow;
        var currentSigningKeyId = string.IsNullOrWhiteSpace(options.CurrentSigningKeyId)
            ? "bootstrap-current"
            : options.CurrentSigningKeyId.Trim();
        var currentSigningKeyMaterial = string.IsNullOrWhiteSpace(options.CurrentSigningKey)
            ? options.SigningKey
            : options.CurrentSigningKey;
        var usesEphemeralCurrentSigningKey = string.IsNullOrWhiteSpace(currentSigningKeyMaterial);
        var resolvedCurrentSigningKeyMaterial = usesEphemeralCurrentSigningKey
            ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            : currentSigningKeyMaterial!;

        var validationKeys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal)
        {
            [currentSigningKeyId] = CreateSigningKey(resolvedCurrentSigningKeyMaterial, currentSigningKeyId),
        };
        var descriptors = new List<BootstrapSigningKeyDescriptor>
        {
            new()
            {
                KeyId = currentSigningKeyId,
                IsCurrent = true,
                IsAcceptedForValidation = true,
            },
        };

        foreach (var additionalKey in options.AdditionalSigningKeys)
        {
            var keyId = NormalizeKeyId(additionalKey.KeyId);
            if (validationKeys.ContainsKey(keyId))
            {
                throw new InvalidOperationException(
                    $"BootstrapOAuth signing key id '{keyId}' is configured more than once.");
            }

            var isAcceptedForValidation = !additionalKey.RetireAtUtc.HasValue || additionalKey.RetireAtUtc.Value > now;
            if (isAcceptedForValidation)
            {
                validationKeys[keyId] = CreateSigningKey(additionalKey.Key, keyId);
            }

            descriptors.Add(new BootstrapSigningKeyDescriptor
            {
                KeyId = keyId,
                IsCurrent = false,
                RetireAtUtc = additionalKey.RetireAtUtc,
                IsAcceptedForValidation = isAcceptedForValidation,
            });
        }

        CurrentSigningKeyId = currentSigningKeyId;
        UsesEphemeralCurrentSigningKey = usesEphemeralCurrentSigningKey;
        CurrentSigningCredentials = new SigningCredentials(
            validationKeys[currentSigningKeyId],
            SecurityAlgorithms.HmacSha256);
        Descriptors = descriptors;
        _validationKeysById = validationKeys;
    }

    public string CurrentSigningKeyId { get; }

    public bool UsesEphemeralCurrentSigningKey { get; }

    public SigningCredentials CurrentSigningCredentials { get; }

    public IReadOnlyCollection<BootstrapSigningKeyDescriptor> Descriptors { get; }

    public IEnumerable<SecurityKey> ResolveValidationKeys(string? kid)
    {
        if (!string.IsNullOrWhiteSpace(kid))
        {
            return _validationKeysById.TryGetValue(kid, out var keyedSigningKey)
                ? [keyedSigningKey]
                : [];
        }

        return _validationKeysById.Values;
    }

    private static string NormalizeKeyId(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new InvalidOperationException("BootstrapOAuth additional signing key id must be configured.");
        }

        return keyId.Trim();
    }

    private static SecurityKey CreateSigningKey(string signingKeyMaterial, string keyId)
    {
        if (string.IsNullOrWhiteSpace(signingKeyMaterial))
        {
            throw new InvalidOperationException(
                $"BootstrapOAuth signing key '{keyId}' must not be empty.");
        }

        if (Encoding.UTF8.GetByteCount(signingKeyMaterial) < 32)
        {
            throw new InvalidOperationException(
                $"BootstrapOAuth signing key '{keyId}' must be at least 32 bytes.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyMaterial))
        {
            KeyId = keyId,
        };

        return key;
    }
}

public sealed record BootstrapSigningKeyDescriptor
{
    public required string KeyId { get; init; }

    public required bool IsCurrent { get; init; }

    public DateTimeOffset? RetireAtUtc { get; init; }

    public required bool IsAcceptedForValidation { get; init; }
}
