namespace OtpAuth.Api.Admin;

public sealed record AdminStartTotpEnrollmentHttpRequest
{
    public required Guid TenantId { get; init; }

    public Guid? ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Issuer { get; init; }

    public string? Label { get; init; }
}

public sealed record AdminConfirmTotpEnrollmentHttpRequest
{
    public required string Code { get; init; }
}

public sealed record AdminTotpEnrollmentCommandHttpResponse
{
    public required Guid EnrollmentId { get; init; }

    public required string Status { get; init; }

    public bool HasPendingReplacement { get; init; }

    public DateTimeOffset? ConfirmedAtUtc { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; init; }

    public string? SecretUri { get; init; }

    public string? QrCodePayload { get; init; }
}
