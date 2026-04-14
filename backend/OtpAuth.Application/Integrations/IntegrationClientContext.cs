namespace OtpAuth.Application.Integrations;

public sealed record IntegrationClientContext
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public IReadOnlyCollection<string> Scopes { get; init; } = Array.Empty<string>();

    public bool HasScope(string scope)
    {
        return Scopes.Contains(scope, StringComparer.Ordinal);
    }
}
