namespace OtpAuth.Application.Factors;

public sealed record TotpEnrollmentSecret
{
    public required Guid EnrollmentId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }

    public required byte[] Secret { get; init; }

    public required int Digits { get; init; }

    public required int PeriodSeconds { get; init; }

    public required string Algorithm { get; init; }

    public required int KeyVersion { get; init; }
}
