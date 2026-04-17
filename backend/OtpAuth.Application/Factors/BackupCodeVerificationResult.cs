namespace OtpAuth.Application.Factors;

public sealed record BackupCodeVerificationResult
{
    public required BackupCodeVerificationStatus Status { get; init; }

    public Guid? BackupCodeId { get; init; }

    public static BackupCodeVerificationResult Valid(Guid backupCodeId) => new()
    {
        Status = BackupCodeVerificationStatus.Valid,
        BackupCodeId = backupCodeId,
    };

    public static BackupCodeVerificationResult InvalidCode() => new()
    {
        Status = BackupCodeVerificationStatus.InvalidCode,
    };
}

public enum BackupCodeVerificationStatus
{
    InvalidCode = 0,
    Valid = 1,
}
