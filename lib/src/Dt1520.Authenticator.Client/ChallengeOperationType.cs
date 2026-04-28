namespace Dt1520.Authenticator.Client;

/// <summary>
/// Protected operation type used by DT-1520 policy evaluation.
/// </summary>
public enum ChallengeOperationType
{
    /// <summary>
    /// Login operation.
    /// </summary>
    Login,

    /// <summary>
    /// Step-up verification for a sensitive operation.
    /// </summary>
    StepUp,

    /// <summary>
    /// Backup-code recovery operation.
    /// </summary>
    BackupCodeRecovery,
}
