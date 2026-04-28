namespace Dt1520.Authenticator.Client;

/// <summary>
/// OAuth 2.0 access token issued by DT-1520 for a trusted integration client.
/// </summary>
public sealed class Dt1520AuthenticatorAccessToken
{
    internal Dt1520AuthenticatorAccessToken(
        string accessToken,
        string tokenType,
        DateTimeOffset expiresAtUtc,
        string? scope)
    {
        AccessToken = accessToken;
        TokenType = tokenType;
        ExpiresAtUtc = expiresAtUtc;
        Scope = scope;
    }

    /// <summary>
    /// Access token value. Do not log or expose this value to browsers or desktop clients.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// OAuth token type returned by DT-1520.
    /// </summary>
    public string TokenType { get; }

    /// <summary>
    /// UTC timestamp after which the token must not be reused.
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; }

    /// <summary>
    /// Scope granted by DT-1520, when returned by the token endpoint.
    /// </summary>
    public string? Scope { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(Dt1520AuthenticatorAccessToken)} {{ {nameof(AccessToken)} = [redacted], {nameof(TokenType)} = {TokenType}, {nameof(ExpiresAtUtc)} = {ExpiresAtUtc:O}, {nameof(Scope)} = {Scope} }}";
    }
}
