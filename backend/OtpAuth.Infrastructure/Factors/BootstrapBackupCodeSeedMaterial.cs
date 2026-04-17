namespace OtpAuth.Infrastructure.Factors;

public sealed record BootstrapBackupCodeSeedMaterial
{
    public required Guid TenantId { get; init; }

    public required Guid ApplicationClientId { get; init; }

    public required string ExternalUserId { get; init; }

    public string? Username { get; init; }

    public required IReadOnlyCollection<string> Codes { get; init; }
}
