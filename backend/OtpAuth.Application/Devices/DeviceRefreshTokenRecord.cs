namespace OtpAuth.Application.Devices;

public sealed record DeviceRefreshTokenRecord
{
    public required Guid TokenId { get; init; }

    public required Guid DeviceId { get; init; }

    public required Guid TokenFamilyId { get; init; }

    public required string TokenHash { get; init; }

    public required DateTimeOffset IssuedUtc { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }

    public DateTimeOffset? ConsumedUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public Guid? ReplacedByTokenId { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}
