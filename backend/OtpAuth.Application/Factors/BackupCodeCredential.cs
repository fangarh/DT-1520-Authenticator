namespace OtpAuth.Application.Factors;

public sealed class BackupCodeCredential
{
    public Guid BackupCodeId { get; set; }

    public string CodeHash { get; set; } = string.Empty;
}
