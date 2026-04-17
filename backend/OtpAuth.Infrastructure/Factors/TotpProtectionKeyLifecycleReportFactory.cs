namespace OtpAuth.Infrastructure.Factors;

public sealed class TotpProtectionKeyLifecycleReportFactory
{
    public TotpProtectionKeyLifecycleReport Create(
        TotpProtectionOptions options,
        IReadOnlyCollection<TotpEnrollmentKeyVersionUsage> usage,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(usage);

        if (options.CurrentKeyVersion <= 0)
        {
            throw new InvalidOperationException("TotpProtection:CurrentKeyVersion must be greater than zero.");
        }

        var configuredKeyVersions = new HashSet<int> { options.CurrentKeyVersion };
        foreach (var additionalKey in options.AdditionalKeys)
        {
            if (additionalKey.KeyVersion <= 0)
            {
                throw new InvalidOperationException("TotpProtection:AdditionalKeys key version must be greater than zero.");
            }

            configuredKeyVersions.Add(additionalKey.KeyVersion);
        }

        var configuredKeys = configuredKeyVersions
            .OrderByDescending(keyVersion => keyVersion == options.CurrentKeyVersion)
            .ThenBy(keyVersion => keyVersion)
            .Select(keyVersion => new TotpProtectionKeyLifecycleReportKey
            {
                KeyVersion = keyVersion,
                IsCurrent = keyVersion == options.CurrentKeyVersion,
            })
            .ToArray();

        var usageByKeyVersion = usage
            .OrderBy(item => item.KeyVersion)
            .Select(item => new TotpProtectionKeyLifecycleReportUsage
            {
                KeyVersion = item.KeyVersion,
                EnrollmentCount = item.EnrollmentCount,
                IsConfigured = configuredKeyVersions.Contains(item.KeyVersion),
                IsCurrent = item.KeyVersion == options.CurrentKeyVersion,
            })
            .ToArray();

        var reencryptionBacklog = usageByKeyVersion
            .Where(item => item.KeyVersion != options.CurrentKeyVersion)
            .Sum(item => item.EnrollmentCount);

        var warnings = BuildWarnings(options.CurrentKeyVersion, configuredKeys, usageByKeyVersion, reencryptionBacklog);

        return new TotpProtectionKeyLifecycleReport
        {
            ObservedAtUtc = observedAtUtc,
            CurrentKeyVersion = options.CurrentKeyVersion,
            ConfiguredKeys = configuredKeys,
            UsageByKeyVersion = usageByKeyVersion,
            EnrollmentsRequiringReEncryptionCount = reencryptionBacklog,
            Warnings = warnings,
        };
    }

    private static IReadOnlyCollection<string> BuildWarnings(
        int currentKeyVersion,
        IReadOnlyCollection<TotpProtectionKeyLifecycleReportKey> configuredKeys,
        IReadOnlyCollection<TotpProtectionKeyLifecycleReportUsage> usage,
        int reencryptionBacklog)
    {
        var warnings = new List<string>();

        if (reencryptionBacklog > 0)
        {
            warnings.Add($"There are {reencryptionBacklog} enrollment(s) that still require re-encryption to current key version {currentKeyVersion}.");
        }

        var unconfiguredKeyVersions = usage
            .Where(item => !item.IsConfigured)
            .Select(item => item.KeyVersion)
            .ToArray();
        if (unconfiguredKeyVersions.Length > 0)
        {
            warnings.Add($"Database contains enrollments for key versions that are not configured in runtime: {string.Join(", ", unconfiguredKeyVersions)}");
        }

        var unusedLegacyKeyVersions = configuredKeys
            .Where(item => !item.IsCurrent && usage.All(usageItem => usageItem.KeyVersion != item.KeyVersion))
            .Select(item => item.KeyVersion)
            .ToArray();
        if (unusedLegacyKeyVersions.Length > 0)
        {
            warnings.Add($"Legacy protection keys are configured without remaining enrollments: {string.Join(", ", unusedLegacyKeyVersions)}");
        }

        return warnings;
    }
}
