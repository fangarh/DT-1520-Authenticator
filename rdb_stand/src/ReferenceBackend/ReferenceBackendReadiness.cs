using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed record ReferenceBackendReadiness
{
    public required bool IsReadyForLiveRun { get; init; }

    public required bool HasTenantId { get; init; }

    public required bool HasApplicationClientId { get; init; }

    public required bool HasCallbackUrl { get; init; }

    public required Uri? CallbackUrl { get; init; }

    public required string CallbackUrlPolicyMode { get; init; }

    public required bool AllowInsecureCallbackHttp { get; init; }

    public required IReadOnlyCollection<string> ConfigurationIssues { get; init; }
}

public sealed class ReferenceBackendReadinessReporter(
    IOptions<ReferenceBackendOptions> options)
{
    private readonly ReferenceBackendOptions _options = options.Value;

    public ReferenceBackendReadiness GetReadiness()
    {
        var issues = _options.Validate();
        ReferenceCallbackUrlPolicy.TryCreateFromOptions(_options, out var policy, out _);

        return new ReferenceBackendReadiness
        {
            IsReadyForLiveRun = issues.Count == 0,
            HasTenantId = _options.TenantId != Guid.Empty,
            HasApplicationClientId = _options.ApplicationClientId != Guid.Empty,
            HasCallbackUrl = _options.CallbackUrl is not null,
            CallbackUrl = _options.CallbackUrl,
            CallbackUrlPolicyMode = policy.ModeName,
            AllowInsecureCallbackHttp = policy.AllowInsecureHttp,
            ConfigurationIssues = issues,
        };
    }
}
