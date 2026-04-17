namespace OtpAuth.Api.Enrollments;

public sealed record StartTotpEnrollmentHttpRequest
{
    public required Guid TenantId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Issuer { get; init; }

    public string? Label { get; init; }
}

public sealed record ConfirmTotpEnrollmentHttpRequest
{
    public required string Code { get; init; }
}

public sealed record TotpEnrollmentHttpResponse
{
    public required Guid EnrollmentId { get; init; }

    public required string Status { get; init; }

    public bool HasPendingReplacement { get; init; }

    public string? SecretUri { get; init; }

    public string? QrCodePayload { get; init; }
}
