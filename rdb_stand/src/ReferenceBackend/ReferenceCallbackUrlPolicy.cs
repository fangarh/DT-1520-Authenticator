using System.Net;
using System.Net.Sockets;

namespace Dt1520.Authenticator.ReferenceBackend;

public enum ReferenceCallbackUrlPolicyMode
{
    PublicInternet = 0,
    PrivateNetwork = 1,
    LocalDevelopment = 2,
}

public sealed class ReferenceCallbackUrlPolicy
{
    public ReferenceCallbackUrlPolicy(
        ReferenceCallbackUrlPolicyMode mode,
        bool allowInsecureHttp)
    {
        Mode = mode;
        AllowInsecureHttp = allowInsecureHttp;
    }

    public ReferenceCallbackUrlPolicyMode Mode { get; }

    public bool AllowInsecureHttp { get; }

    public string ModeName => Mode.ToString();

    public static ReferenceCallbackUrlPolicy FromOptions(ReferenceBackendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!TryCreateFromOptions(options, out var policy, out _))
        {
            throw new InvalidOperationException(
                "ReferenceBackend:CallbackUrlPolicyMode must be one of: PublicInternet, PrivateNetwork, LocalDevelopment.");
        }

        return policy;
    }

    public static bool TryCreateFromOptions(
        ReferenceBackendOptions options,
        out ReferenceCallbackUrlPolicy policy,
        out string? failure)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Enum.TryParse(options.CallbackUrlPolicyMode?.Trim(), ignoreCase: true, out ReferenceCallbackUrlPolicyMode mode) ||
            !Enum.IsDefined(typeof(ReferenceCallbackUrlPolicyMode), mode))
        {
            policy = new ReferenceCallbackUrlPolicy(ReferenceCallbackUrlPolicyMode.PublicInternet, allowInsecureHttp: false);
            failure = "CallbackUrlPolicyMode must be one of: PublicInternet, PrivateNetwork, LocalDevelopment.";
            return false;
        }

        policy = new ReferenceCallbackUrlPolicy(mode, options.AllowInsecureCallbackHttp);
        failure = null;
        return true;
    }

    public string? Validate(Uri callbackUrl)
    {
        ArgumentNullException.ThrowIfNull(callbackUrl);

        var failure = ValidateCore(callbackUrl);
        return failure is null
            ? null
            : $"CallbackUrl is rejected by {ModeName} policy: {failure}";
    }

    private string? ValidateCore(Uri callbackUrl)
    {
        if (!callbackUrl.IsAbsoluteUri)
        {
            return "absolute URI is required.";
        }

        if (!IsHttpScheme(callbackUrl))
        {
            return "HTTP(S) scheme is required.";
        }

        if (callbackUrl.Scheme == Uri.UriSchemeHttp && !AllowsHttp(callbackUrl))
        {
            return "HTTPS is required.";
        }

        if (!string.IsNullOrEmpty(callbackUrl.UserInfo))
        {
            return "embedded credentials are not allowed.";
        }

        if (string.IsNullOrWhiteSpace(callbackUrl.AbsolutePath) || callbackUrl.AbsolutePath == "/")
        {
            return "non-root callback path is required.";
        }

        if (!string.IsNullOrEmpty(callbackUrl.Fragment))
        {
            return "URL fragments are not allowed.";
        }

        var hostKind = ClassifyHost(callbackUrl.Host);
        return Mode switch
        {
            ReferenceCallbackUrlPolicyMode.PublicInternet when hostKind is CallbackHostKind.Localhost or CallbackHostKind.LoopbackIp or CallbackHostKind.PrivateOrLoopbackIp =>
                "localhost and private network IP literals are not allowed.",
            ReferenceCallbackUrlPolicyMode.PrivateNetwork when hostKind is CallbackHostKind.Localhost or CallbackHostKind.LoopbackIp =>
                "localhost and loopback IP literals are not allowed.",
            _ => null,
        };
    }

    private bool AllowsHttp(Uri callbackUrl)
    {
        if (Mode == ReferenceCallbackUrlPolicyMode.LocalDevelopment)
        {
            return true;
        }

        if (Mode != ReferenceCallbackUrlPolicyMode.PrivateNetwork || !AllowInsecureHttp)
        {
            return false;
        }

        var hostKind = ClassifyHost(callbackUrl.Host);
        return hostKind is CallbackHostKind.PrivateOrLoopbackIp or CallbackHostKind.PrivateDns;
    }

    private static bool IsHttpScheme(Uri callbackUrl)
    {
        return string.Equals(callbackUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(callbackUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

    private static CallbackHostKind ClassifyHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return CallbackHostKind.Localhost;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return host.Contains('.', StringComparison.Ordinal)
                ? CallbackHostKind.PublicDns
                : CallbackHostKind.PrivateDns;
        }

        if (IPAddress.IsLoopback(address))
        {
            return CallbackHostKind.LoopbackIp;
        }

        return IsPrivateIp(address)
            ? CallbackHostKind.PrivateOrLoopbackIp
            : CallbackHostKind.PublicIp;
    }

    private static bool IsPrivateIp(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168
                || bytes[0] == 169 && bytes[1] == 254;
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;
    }

    private enum CallbackHostKind
    {
        PublicDns,
        PrivateDns,
        PublicIp,
        PrivateOrLoopbackIp,
        LoopbackIp,
        Localhost,
    }
}
