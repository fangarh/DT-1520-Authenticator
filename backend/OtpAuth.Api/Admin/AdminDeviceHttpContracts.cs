namespace OtpAuth.Api.Admin;

public sealed record AdminUserDeviceHttpResponse
{
    public required Guid DeviceId { get; init; }

    public required string Platform { get; init; }

    public required string Status { get; init; }

    public required bool IsPushCapable { get; init; }

    public DateTimeOffset? ActivatedAtUtc { get; init; }

    public DateTimeOffset? LastSeenAtUtc { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; init; }

    public DateTimeOffset? BlockedAtUtc { get; init; }
}
