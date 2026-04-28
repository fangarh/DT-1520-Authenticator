using Dt1520.Authenticator.AspNetCore;

namespace Dt1520.Authenticator.ReferenceBackend;

public sealed record LiveRunPreflightReport
{
    public required bool IsReadyForBackendStart { get; init; }

    public required bool HasDt1520BaseUrl { get; init; }

    public required string? Dt1520BaseUrlHost { get; init; }

    public required bool HasClientId { get; init; }

    public required bool HasClientSecret { get; init; }

    public required bool HasCallbackSigningSecret { get; init; }

    public required bool HasTenantId { get; init; }

    public required bool HasApplicationClientId { get; init; }

    public required bool HasCallbackUrl { get; init; }

    public required string? CallbackUrlHost { get; init; }

    public required IReadOnlyCollection<string> ConfigurationIssues { get; init; }
}

public static class LiveRunPreflightReporter
{
    public static LiveRunPreflightReport Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var authenticator = configuration
            .GetSection("Dt1520Authenticator")
            .Get<Dt1520AuthenticatorAspNetCoreOptions>() ?? new Dt1520AuthenticatorAspNetCoreOptions();
        var reference = configuration
            .GetSection("ReferenceBackend")
            .Get<ReferenceBackendOptions>() ?? new ReferenceBackendOptions();
        var issues = Validate(authenticator, reference);

        return new LiveRunPreflightReport
        {
            IsReadyForBackendStart = issues.Count == 0,
            HasDt1520BaseUrl = authenticator.BaseUrl is not null,
            Dt1520BaseUrlHost = authenticator.BaseUrl?.Host,
            HasClientId = !string.IsNullOrWhiteSpace(authenticator.ClientId),
            HasClientSecret = !string.IsNullOrWhiteSpace(authenticator.ClientSecret),
            HasCallbackSigningSecret = !string.IsNullOrWhiteSpace(authenticator.CallbackSigningSecret),
            HasTenantId = reference.TenantId != Guid.Empty,
            HasApplicationClientId = reference.ApplicationClientId != Guid.Empty,
            HasCallbackUrl = reference.CallbackUrl is not null,
            CallbackUrlHost = reference.CallbackUrl?.Host,
            ConfigurationIssues = issues,
        };
    }

    private static IReadOnlyCollection<string> Validate(
        Dt1520AuthenticatorAspNetCoreOptions authenticator,
        ReferenceBackendOptions reference)
    {
        List<string> issues = [];

        if (authenticator.BaseUrl is null)
        {
            issues.Add("Dt1520Authenticator:BaseUrl is required.");
        }
        else if (!authenticator.BaseUrl.IsAbsoluteUri
            || authenticator.BaseUrl.Scheme is not ("http" or "https"))
        {
            issues.Add("Dt1520Authenticator:BaseUrl must be an absolute HTTP(S) URL.");
        }

        AddRequired(authenticator.ClientId, "Dt1520Authenticator:ClientId", issues);
        AddRequired(authenticator.ClientSecret, "Dt1520Authenticator:ClientSecret", issues);
        AddRequired(authenticator.CallbackSigningSecret, "Dt1520Authenticator:CallbackSigningSecret", issues);
        issues.AddRange(reference.Validate().Select(issue => $"ReferenceBackend:{issue}"));

        return issues;
    }

    private static void AddRequired(string? value, string name, List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add($"{name} is required.");
        }
    }
}
