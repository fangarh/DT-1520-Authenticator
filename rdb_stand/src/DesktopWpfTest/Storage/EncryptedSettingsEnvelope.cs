namespace Dt1520.Authenticator.DesktopWpfTest.Storage;

public sealed record EncryptedSettingsEnvelope
{
    public int Version { get; init; } = 1;

    public required string Salt { get; init; }

    public required string Nonce { get; init; }

    public required string Tag { get; init; }

    public required string Ciphertext { get; init; }
}
