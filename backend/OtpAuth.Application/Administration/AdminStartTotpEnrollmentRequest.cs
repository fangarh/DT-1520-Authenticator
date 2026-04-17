namespace OtpAuth.Application.Administration;

public sealed record AdminStartTotpEnrollmentRequest
{
    public required Guid TenantId { get; init; }

    public Guid? ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Issuer { get; init; }

    public string? Label { get; init; }
}
