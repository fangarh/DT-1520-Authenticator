namespace Dt1520.Authenticator.Client;

/// <summary>
/// Second-factor type requested or selected for a DT-1520 challenge.
/// </summary>
public enum ChallengeFactorType
{
    /// <summary>
    /// Time-based one-time password factor.
    /// </summary>
    Totp,

    /// <summary>
    /// Device-bound push approval factor.
    /// </summary>
    Push,

    /// <summary>
    /// Backup recovery code factor.
    /// </summary>
    BackupCode,
}
