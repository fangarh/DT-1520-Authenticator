namespace OtpAuth.Infrastructure.Factors;

public sealed record TotpProtectionOptions
{
    public string? CurrentKey { get; init; }

    public int CurrentKeyVersion { get; init; } = 1;

    public IReadOnlyCollection<TotpProtectionKeyOptions> AdditionalKeys { get; init; } = Array.Empty<TotpProtectionKeyOptions>();
}

public sealed record TotpProtectionKeyOptions
{
    public required int KeyVersion { get; init; }

    public required string Key { get; init; }
}
