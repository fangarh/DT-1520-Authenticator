namespace Dt1520.Authenticator.Client;

internal interface IDt1520AuthenticatorClock
{
    DateTimeOffset UtcNow { get; }
}
