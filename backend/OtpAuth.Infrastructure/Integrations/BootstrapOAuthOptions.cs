namespace OtpAuth.Infrastructure.Integrations;

public sealed record BootstrapOAuthOptions
{
    public string Issuer { get; init; } = "otpauth-bootstrap";

    public string Audience { get; init; } = "otpauth-api";

    public int AccessTokenLifetimeMinutes { get; init; } = 60;

    public string? SigningKey { get; init; }

    public string CurrentSigningKeyId { get; init; } = "bootstrap-current";

    public string? CurrentSigningKey { get; init; }

    public IReadOnlyCollection<BootstrapOAuthSigningKeyOptions> AdditionalSigningKeys { get; init; } = Array.Empty<BootstrapOAuthSigningKeyOptions>();

    public IReadOnlyCollection<BootstrapOAuthClientOptions> Clients { get; init; } = Array.Empty<BootstrapOAuthClientOptions>();
}

public sealed record BootstrapOAuthSigningKeyOptions
{
    public required string KeyId { get; init; }

    public required string Key { get; init; }

    public DateTimeOffset? RetireAtUtc { get; init; }
}

public sealed record BootstrapOAuthClientOptions
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ClientSecretEnvVarName { get; init; }

    public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();
}
