namespace OtpAuth.Api.Admin;

public sealed record AdminRuntimeConfigurationHttpResponse
{
    public required AdminCallbackUrlPolicyHttpResponse CallbackUrlPolicy { get; init; }
}

public sealed record AdminCallbackUrlPolicyHttpResponse
{
    public required string Mode { get; init; }

    public required bool AllowInsecureHttp { get; init; }
}
