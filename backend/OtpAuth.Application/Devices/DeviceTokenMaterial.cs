namespace OtpAuth.Application.Devices;

public sealed record DeviceTokenMaterial
{
    public required Guid RefreshTokenId { get; init; }

    public required string RefreshTokenSecret { get; init; }

    public required DateTimeOffset RefreshTokenExpiresUtc { get; init; }

    public required DeviceTokenPair TokenPair { get; init; }
}
