namespace OtpAuth.Api.Admin;

public sealed record AdminDeviceOnboardingArtifactHttpResponse
{
    public required Guid ActivationCodeId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public required string Platform { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public DateTimeOffset? ConsumedAtUtc { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record AdminCreateDeviceOnboardingArtifactHttpRequest
{
    public Guid TenantId { get; init; }

    public Guid ApplicationClientId { get; init; }

    public string? ExternalUserId { get; init; }

    public string? Platform { get; init; }

    public int? TtlMinutes { get; init; }

    public string? ActivationPayload { get; init; }
}

public sealed record AdminCreateDeviceOnboardingArtifactHttpResponse
{
    public required AdminDeviceOnboardingArtifactHttpResponse Artifact { get; init; }

    public required string ActivationPayload { get; init; }
}
