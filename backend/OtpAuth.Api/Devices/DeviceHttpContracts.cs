namespace OtpAuth.Api.Devices;

public sealed record ActivateDeviceHttpRequest
{
    public required Guid TenantId { get; init; }

    public required string ExternalUserId { get; init; }

    public required string Platform { get; init; }

    public required string ActivationCode { get; init; }

    public required string InstallationId { get; init; }

    public string? DeviceName { get; init; }

    public string? PushToken { get; init; }

    public string? PublicKey { get; init; }
}

public sealed record RefreshDeviceTokenHttpRequest
{
    public required string RefreshToken { get; init; }
}

public sealed record DeviceHttpResponse
{
    public required Guid Id { get; init; }

    public required string Platform { get; init; }

    public required string Status { get; init; }

    public required string AttestationStatus { get; init; }

    public string? DeviceName { get; init; }

    public required bool IsPushCapable { get; init; }

    public DateTimeOffset? ActivatedAt { get; init; }

    public DateTimeOffset? LastSeenAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public DateTimeOffset? BlockedAt { get; init; }
}

public sealed record DeviceTokenHttpResponse
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required string TokenType { get; init; }

    public required int ExpiresIn { get; init; }

    public required string Scope { get; init; }
}

public sealed record DeviceActivationHttpResponse
{
    public required DeviceHttpResponse Device { get; init; }

    public required DeviceTokenHttpResponse Tokens { get; init; }
}
