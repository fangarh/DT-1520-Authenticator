using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed class ReferenceBackendOptions
{
    public Guid TenantId { get; init; }

    public Guid ApplicationClientId { get; init; }

    public Uri? CallbackUrl { get; init; }

    public TimeSpan DefaultOperationTtl { get; init; } = TimeSpan.FromMinutes(5);

    public IReadOnlyCollection<string> Validate()
    {
        List<string> failures = [];

        if (TenantId == Guid.Empty)
        {
            failures.Add("TenantId is required.");
        }

        if (ApplicationClientId == Guid.Empty)
        {
            failures.Add("ApplicationClientId is required.");
        }

        if (CallbackUrl is null || !CallbackUrl.IsAbsoluteUri)
        {
            failures.Add("CallbackUrl must be an absolute URI.");
        }
        else
        {
            AddCallbackUrlFailures(CallbackUrl, failures);
        }

        if (DefaultOperationTtl <= TimeSpan.Zero)
        {
            failures.Add("DefaultOperationTtl must be positive.");
        }

        return failures;
    }

    private static void AddCallbackUrlFailures(Uri callbackUrl, List<string> failures)
    {
        if (callbackUrl.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("CallbackUrl must use HTTPS.");
        }

        if (!string.IsNullOrEmpty(callbackUrl.UserInfo))
        {
            failures.Add("CallbackUrl must not contain embedded credentials.");
        }

        if (string.Equals(callbackUrl.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(callbackUrl.Host, out var address) && IsPrivateOrLoopback(address))
        {
            failures.Add("CallbackUrl must be externally reachable and must not use localhost or private IP literals.");
        }
    }

    private static bool IsPrivateOrLoopback(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

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
}

public sealed class ReferenceBackendOptionsValidator : IValidateOptions<ReferenceBackendOptions>
{
    public ValidateOptionsResult Validate(string? name, ReferenceBackendOptions options)
    {
        var failures = options.Validate();
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
