namespace Dt1520.Authenticator.Client;

/// <summary>
/// Client credentials used by a trusted backend integration to request DT-1520 access tokens.
/// </summary>
public sealed record Dt1520AuthenticatorClientCredentials
{
    /// <summary>
    /// Creates integration client credentials.
    /// </summary>
    /// <param name="clientId">Integration client identifier issued by DT-1520.</param>
    /// <param name="clientSecret">Integration client secret. Store it only on trusted backend infrastructure.</param>
    public Dt1520AuthenticatorClientCredentials(string clientId, string clientSecret)
    {
        ClientId = clientId;
        ClientSecret = clientSecret;
    }

    /// <summary>
    /// Integration client identifier issued by DT-1520.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Integration client secret. This value is never included in <see cref="ToString"/>.
    /// </summary>
    public string ClientSecret { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(Dt1520AuthenticatorClientCredentials)} {{ {nameof(ClientId)} = {ClientId}, {nameof(ClientSecret)} = [redacted] }}";
    }
}
