namespace OtpAuth.Api.Admin;

public sealed record AdminTotpEnrollmentCurrentHttpResponse
{
    public required Guid EnrollmentId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Label { get; init; }

    public required string Status { get; init; }

    public bool HasPendingReplacement { get; init; }

    public DateTimeOffset? ConfirmedAtUtc { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; init; }
}
