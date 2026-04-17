namespace OtpAuth.Infrastructure.Integrations;

public sealed record BootstrapSigningKeyLifecycleReport
{
    public required DateTimeOffset ObservedAtUtc { get; init; }

    public required string CurrentSigningKeyId { get; init; }

    public required int AccessTokenLifetimeMinutes { get; init; }

    public required int RecommendedLegacyRetirementDelaySeconds { get; init; }

    public required bool UsesEphemeralCurrentSigningKey { get; init; }

    public IReadOnlyCollection<BootstrapSigningKeyLifecycleReportKey> Keys { get; init; } =
        Array.Empty<BootstrapSigningKeyLifecycleReportKey>();

    public IReadOnlyCollection<string> Warnings { get; init; } = Array.Empty<string>();

    public int ActiveLegacyKeyCount => Keys.Count(key => !key.IsCurrent && key.IsAcceptedForValidation);

    public int RetiredLegacyKeyCount => Keys.Count(key => !key.IsCurrent && !key.IsAcceptedForValidation);

    public string Summary =>
        $"current={CurrentSigningKeyId}; active_legacy={ActiveLegacyKeyCount}; retired_legacy={RetiredLegacyKeyCount}; warnings={Warnings.Count}";
}

public sealed record BootstrapSigningKeyLifecycleReportKey
{
    public required string KeyId { get; init; }

    public required bool IsCurrent { get; init; }

    public DateTimeOffset? RetireAtUtc { get; init; }

    public required bool IsAcceptedForValidation { get; init; }
}
