namespace OtpAuth.Domain.Challenges;

public enum ChallengeStatus
{
    Unknown = 0,
    Pending = 1,
    Approved = 2,
    Denied = 3,
    Expired = 4,
    Failed = 5,
}
