namespace OtpAuth.Application.Enrollments;

public enum TotpEnrollmentStatus
{
    Unknown = 0,
    Pending = 1,
    Confirmed = 2,
    Revoked = 3,
}

public sealed record StartTotpEnrollmentRequest
{
    public required Guid TenantId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Issuer { get; init; }

    public string? Label { get; init; }
}

public sealed record ConfirmTotpEnrollmentRequest
{
    public required Guid EnrollmentId { get; init; }

    public required string Code { get; init; }
}

public sealed record TotpEnrollmentView
{
    public required Guid EnrollmentId { get; init; }

    public required TotpEnrollmentStatus Status { get; init; }

    public bool HasPendingReplacement { get; init; }

    public DateTimeOffset? ConfirmedAtUtc { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; init; }

    public string? SecretUri { get; init; }

    public string? QrCodePayload { get; init; }
}

public sealed record TotpEnrollmentAdminView
{
    public required Guid EnrollmentId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Label { get; init; }

    public required TotpEnrollmentStatus Status { get; init; }

    public bool HasPendingReplacement { get; init; }

    public DateTimeOffset? ConfirmedAtUtc { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; init; }
}

public sealed record TotpPendingReplacementRecord
{
    public required byte[] Secret { get; init; }

    public required int Digits { get; init; }

    public required int PeriodSeconds { get; init; }

    public required string Algorithm { get; init; }

    public required DateTimeOffset StartedUtc { get; init; }

    public int FailedConfirmationAttempts { get; init; }
}

public sealed record TotpEnrollmentProvisioningRecord
{
    public required Guid EnrollmentId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Label { get; init; }

    public required byte[] Secret { get; init; }

    public required int Digits { get; init; }

    public required int PeriodSeconds { get; init; }

    public required string Algorithm { get; init; }

    public required bool IsActive { get; init; }

    public DateTimeOffset? ConfirmedUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public int FailedConfirmationAttempts { get; init; }

    public TotpPendingReplacementRecord? PendingReplacement { get; init; }

    public TotpEnrollmentStatus Status => !IsActive
        ? TotpEnrollmentStatus.Revoked
        : ConfirmedUtc.HasValue
            ? TotpEnrollmentStatus.Confirmed
            : TotpEnrollmentStatus.Pending;

    public bool HasPendingReplacement => PendingReplacement is not null;
}

public sealed record TotpEnrollmentProvisioningDraft
{
    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Label { get; init; }

    public required byte[] Secret { get; init; }

    public required int Digits { get; init; }

    public required int PeriodSeconds { get; init; }

    public required string Algorithm { get; init; }
}

public sealed record TotpEnrollmentReplacementDraft
{
    public required Guid EnrollmentId { get; init; }

    public required byte[] Secret { get; init; }

    public required int Digits { get; init; }

    public required int PeriodSeconds { get; init; }

    public required string Algorithm { get; init; }
}
