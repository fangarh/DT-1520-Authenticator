using System.Net;
using System.Net.Sockets;

namespace OtpAuth.Application.Challenges;

public enum ChallengeCallbackUrlPolicyMode
{
    PublicInternet = 0,
    PrivateNetwork = 1,
    LocalDevelopment = 2,
}

public sealed record ChallengeCallbackUrlPolicyOptions
{
    public string Mode { get; init; } = nameof(ChallengeCallbackUrlPolicyMode.PublicInternet);

    public bool AllowInsecureHttp { get; init; }
}

public sealed record ChallengeCallbackUrlValidationResult
{
    public required bool IsValid { get; init; }

    public string? ErrorMessage { get; init; }

    public static ChallengeCallbackUrlValidationResult Success() => new()
    {
        IsValid = true,
    };

    public static ChallengeCallbackUrlValidationResult Failure(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage,
    };
}

public sealed class ChallengeCallbackUrlPolicy
{
    public ChallengeCallbackUrlPolicy(
        ChallengeCallbackUrlPolicyMode mode,
        bool allowInsecureHttp = false)
    {
        Mode = mode;
        AllowInsecureHttp = allowInsecureHttp;
    }

    public ChallengeCallbackUrlPolicyMode Mode { get; }

    public bool AllowInsecureHttp { get; }

    public string ModeName => Mode.ToString();

    public static ChallengeCallbackUrlPolicy PublicInternet { get; } = new(ChallengeCallbackUrlPolicyMode.PublicInternet);

    public static ChallengeCallbackUrlPolicy FromOptions(ChallengeCallbackUrlPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!TryParseMode(options.Mode, out var mode))
        {
            throw new InvalidOperationException(
                "ChallengeCallbackUrlPolicy:Mode must be one of: PublicInternet, PrivateNetwork, LocalDevelopment.");
        }

        return new ChallengeCallbackUrlPolicy(mode, options.AllowInsecureHttp);
    }

    public ChallengeCallbackUrlValidationResult Validate(Uri callbackUrl)
    {
        ArgumentNullException.ThrowIfNull(callbackUrl);

        var failure = ValidateCore(callbackUrl);
        return failure is null
            ? ChallengeCallbackUrlValidationResult.Success()
            : ChallengeCallbackUrlValidationResult.Failure($"CallbackUrl is rejected by {ModeName} policy: {failure}");
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
            ChallengeCallbackUrlPolicyMode.PublicInternet when hostKind is CallbackHostKind.Localhost or CallbackHostKind.LoopbackIp or CallbackHostKind.PrivateOrLoopbackIp =>
                "localhost and private network IP literals are not allowed.",
            ChallengeCallbackUrlPolicyMode.PrivateNetwork when hostKind is CallbackHostKind.Localhost or CallbackHostKind.LoopbackIp =>
                "localhost and loopback IP literals are not allowed.",
            _ => null,
        };
    }

    private bool AllowsHttp(Uri callbackUrl)
    {
        if (Mode == ChallengeCallbackUrlPolicyMode.LocalDevelopment)
        {
            return true;
        }

        if (Mode != ChallengeCallbackUrlPolicyMode.PrivateNetwork || !AllowInsecureHttp)
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

    private static bool TryParseMode(string? value, out ChallengeCallbackUrlPolicyMode mode)
    {
        return Enum.TryParse(value?.Trim(), ignoreCase: true, out mode) &&
               Enum.IsDefined(typeof(ChallengeCallbackUrlPolicyMode), mode);
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
