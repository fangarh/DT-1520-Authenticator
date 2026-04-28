using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed class ReferenceBackendOptions
{
    public Guid TenantId { get; init; }

    public Guid ApplicationClientId { get; init; }

    public Uri? CallbackUrl { get; init; }

    public string CallbackUrlPolicyMode { get; init; } = nameof(ReferenceCallbackUrlPolicyMode.PublicInternet);

    public bool AllowInsecureCallbackHttp { get; init; }

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
            if (!ReferenceCallbackUrlPolicy.TryCreateFromOptions(this, out var policy, out var policyFailure))
            {
                failures.Add(policyFailure!);
            }
            else if (policy.Validate(CallbackUrl) is string callbackUrlFailure)
            {
                failures.Add(callbackUrlFailure);
            }
        }

        if (DefaultOperationTtl <= TimeSpan.Zero)
        {
            failures.Add("DefaultOperationTtl must be positive.");
        }

        return failures;
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
