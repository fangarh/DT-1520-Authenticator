namespace OtpAuth.Infrastructure.Administration;

public sealed record AdminUserBootstrapMaterial
{
    public required string Username { get; init; }

    public required string NormalizedUsername { get; init; }

    public required string PasswordHash { get; init; }

    public required IReadOnlyCollection<string> Permissions { get; init; }
}
