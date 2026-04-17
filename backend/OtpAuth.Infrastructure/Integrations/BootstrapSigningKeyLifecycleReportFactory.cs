namespace OtpAuth.Infrastructure.Integrations;

public sealed class BootstrapSigningKeyLifecycleReportFactory
{
    public BootstrapSigningKeyLifecycleReport Create(
        BootstrapOAuthOptions options,
        BootstrapSigningKeyRing keyRing,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(keyRing);

        var keys = keyRing.Descriptors
            .OrderByDescending(descriptor => descriptor.IsCurrent)
            .ThenBy(descriptor => descriptor.KeyId, StringComparer.Ordinal)
            .Select(descriptor => new BootstrapSigningKeyLifecycleReportKey
            {
                KeyId = descriptor.KeyId,
                IsCurrent = descriptor.IsCurrent,
                RetireAtUtc = descriptor.RetireAtUtc,
                IsAcceptedForValidation = descriptor.IsAcceptedForValidation,
            })
            .ToArray();

        var warnings = BuildWarnings(keyRing, keys);
        return new BootstrapSigningKeyLifecycleReport
        {
            ObservedAtUtc = observedAtUtc,
            CurrentSigningKeyId = keyRing.CurrentSigningKeyId,
            AccessTokenLifetimeMinutes = options.AccessTokenLifetimeMinutes,
            RecommendedLegacyRetirementDelaySeconds = (int)(TimeSpan.FromMinutes(options.AccessTokenLifetimeMinutes) + TimeSpan.FromSeconds(30)).TotalSeconds,
            UsesEphemeralCurrentSigningKey = keyRing.UsesEphemeralCurrentSigningKey,
            Keys = keys,
            Warnings = warnings,
        };
    }

    private static IReadOnlyCollection<string> BuildWarnings(
        BootstrapSigningKeyRing keyRing,
        IReadOnlyCollection<BootstrapSigningKeyLifecycleReportKey> keys)
    {
        var warnings = new List<string>();
        if (keyRing.UsesEphemeralCurrentSigningKey)
        {
            warnings.Add("Current signing key is ephemeral and changes on restart.");
        }

        var nonRetiringLegacyKeys = keys
            .Where(key => !key.IsCurrent && key.IsAcceptedForValidation && key.RetireAtUtc is null)
            .Select(key => key.KeyId)
            .ToArray();
        if (nonRetiringLegacyKeys.Length > 0)
        {
            warnings.Add($"Legacy keys without RetireAtUtc remain valid indefinitely: {string.Join(", ", nonRetiringLegacyKeys)}");
        }

        var retiredLegacyKeysStillConfigured = keys
            .Where(key => !key.IsCurrent && !key.IsAcceptedForValidation)
            .Select(key => key.KeyId)
            .ToArray();
        if (retiredLegacyKeysStillConfigured.Length > 0)
        {
            warnings.Add($"Retired legacy keys are still present in configuration and should be removed: {string.Join(", ", retiredLegacyKeysStillConfigured)}");
        }

        return warnings;
    }
}
