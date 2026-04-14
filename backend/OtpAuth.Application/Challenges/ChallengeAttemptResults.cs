namespace OtpAuth.Application.Challenges;

public static class ChallengeAttemptResults
{
    public const string Approved = "approved";
    public const string InvalidCode = "invalid_code";
    public const string ReplayDetected = "replay_detected";
    public const string Expired = "expired";
    public const string InvalidState = "invalid_state";
    public const string UnsupportedFactor = "unsupported_factor";
    public const string RateLimited = "rate_limited";
}
