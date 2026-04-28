namespace Dt1520.Authenticator.Client;

/// <summary>
/// Push challenge outcome helper for integrator workflow code.
/// </summary>
public sealed record PushChallengeOutcome
{
    private PushChallengeOutcome(PushChallengeOutcomeKind kind, bool isTerminal)
    {
        Kind = kind;
        IsTerminal = isTerminal;
    }

    /// <summary>
    /// Normalized outcome kind.
    /// </summary>
    public PushChallengeOutcomeKind Kind { get; }

    /// <summary>
    /// Indicates whether the challenge no longer needs polling or user waiting UI.
    /// </summary>
    public bool IsTerminal { get; }

    /// <summary>
    /// Converts a DT-1520 challenge response into a push workflow outcome.
    /// </summary>
    public static PushChallengeOutcome FromChallenge(ChallengeResponse challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        return challenge.Status switch
        {
            ChallengeStatus.Pending => new PushChallengeOutcome(PushChallengeOutcomeKind.Pending, isTerminal: false),
            ChallengeStatus.Approved => new PushChallengeOutcome(PushChallengeOutcomeKind.Approved, isTerminal: true),
            ChallengeStatus.Denied => new PushChallengeOutcome(PushChallengeOutcomeKind.Denied, isTerminal: true),
            ChallengeStatus.Expired => new PushChallengeOutcome(PushChallengeOutcomeKind.Expired, isTerminal: true),
            ChallengeStatus.Failed => new PushChallengeOutcome(PushChallengeOutcomeKind.Failed, isTerminal: true),
            _ => new PushChallengeOutcome(PushChallengeOutcomeKind.Failed, isTerminal: true),
        };
    }
}
