namespace OtpAuth.Infrastructure.Factors;

public sealed record TotpProtectionKeyLifecycleReport
{
    public required DateTimeOffset ObservedAtUtc { get; init; }

    public required int CurrentKeyVersion { get; init; }

    public IReadOnlyCollection<TotpProtectionKeyLifecycleReportKey> ConfiguredKeys { get; init; } =
        Array.Empty<TotpProtectionKeyLifecycleReportKey>();

    public IReadOnlyCollection<TotpProtectionKeyLifecycleReportUsage> UsageByKeyVersion { get; init; } =
        Array.Empty<TotpProtectionKeyLifecycleReportUsage>();

    public required int EnrollmentsRequiringReEncryptionCount { get; init; }

    public IReadOnlyCollection<string> Warnings { get; init; } = Array.Empty<string>();

    public int ActiveLegacyKeyCount => ConfiguredKeys.Count(key => !key.IsCurrent);

    public string Summary =>
        $"current_version={CurrentKeyVersion}; active_legacy={ActiveLegacyKeyCount}; reencryption_backlog={EnrollmentsRequiringReEncryptionCount}; warnings={Warnings.Count}";
}

public sealed record TotpProtectionKeyLifecycleReportKey
{
    public required int KeyVersion { get; init; }

    public required bool IsCurrent { get; init; }
}

public sealed record TotpProtectionKeyLifecycleReportUsage
{
    public required int KeyVersion { get; init; }

    public required int EnrollmentCount { get; init; }

    public required bool IsConfigured { get; init; }

    public required bool IsCurrent { get; init; }
}
