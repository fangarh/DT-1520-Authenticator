namespace Dt1520.Authenticator.Client;

internal sealed class SystemDt1520AuthenticatorClock : IDt1520AuthenticatorClock
{
    public static readonly SystemDt1520AuthenticatorClock Instance = new();

    private SystemDt1520AuthenticatorClock()
    {
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
