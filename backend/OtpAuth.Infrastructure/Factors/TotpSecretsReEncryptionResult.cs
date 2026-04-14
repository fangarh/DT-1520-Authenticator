namespace OtpAuth.Infrastructure.Factors;

public sealed record TotpSecretsReEncryptionResult
{
    public required int ScannedRecords { get; init; }

    public required int ReEncryptedRecords { get; init; }

    public required int SkippedRecords { get; init; }
}
