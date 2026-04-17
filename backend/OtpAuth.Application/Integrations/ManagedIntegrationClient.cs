namespace OtpAuth.Application.Integrations;

public sealed record ManagedIntegrationClient
{
    public required string ClientId { get; init; }

    public required bool IsActive { get; init; }

    public DateTimeOffset? LastSecretRotatedUtc { get; init; }

    public DateTimeOffset LastAuthStateChangedUtc { get; init; }
}
