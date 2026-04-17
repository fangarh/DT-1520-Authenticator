namespace OtpAuth.Infrastructure.Administration;

public sealed record AdminUserBootstrapSummary
{
    public required Guid AdminUserId { get; init; }

    public required string Username { get; init; }

    public required bool IsActive { get; init; }

    public required IReadOnlyCollection<string> Permissions { get; init; }
}
