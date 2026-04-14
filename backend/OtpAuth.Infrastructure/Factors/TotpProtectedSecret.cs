namespace OtpAuth.Infrastructure.Factors;

public sealed record TotpProtectedSecret
{
    public required byte[] Ciphertext { get; init; }

    public required byte[] Nonce { get; init; }

    public required byte[] Tag { get; init; }

    public required int KeyVersion { get; init; }
}
