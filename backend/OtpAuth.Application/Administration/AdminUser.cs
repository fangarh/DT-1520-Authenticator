namespace OtpAuth.Application.Administration;

public sealed record AdminUser
{
    public required Guid AdminUserId { get; init; }

    public required string Username { get; init; }

    public required string NormalizedUsername { get; init; }

    public required string PasswordHash { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();
}
