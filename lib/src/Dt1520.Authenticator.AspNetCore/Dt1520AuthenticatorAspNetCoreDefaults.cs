namespace Dt1520.Authenticator.AspNetCore;

/// <summary>
/// Default names used by the DT-1520 Authenticator ASP.NET Core package.
/// </summary>
public static class Dt1520AuthenticatorAspNetCoreDefaults
{
    /// <summary>
    /// Default configuration section name for DT-1520 Authenticator integration settings.
    /// </summary>
    public const string ConfigurationSectionName = "Dt1520Authenticator";

    /// <summary>
    /// Named <see cref="HttpClient"/> registration used by the SDK.
    /// </summary>
    public const string HttpClientName = "Dt1520.Authenticator";
}
