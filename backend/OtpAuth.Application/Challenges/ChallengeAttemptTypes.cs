namespace OtpAuth.Application.Challenges;

public static class ChallengeAttemptTypes
{
    public const string TotpVerify = "totp_verify";
    public const string BackupCodeVerify = "backup_code_verify";
    public const string PushApprove = "push_approve";
    public const string PushDeny = "push_deny";
}
