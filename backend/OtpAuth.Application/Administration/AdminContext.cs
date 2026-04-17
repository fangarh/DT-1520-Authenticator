namespace OtpAuth.Application.Administration;

public sealed record AdminContext
{
    public required Guid AdminUserId { get; init; }

    public required string Username { get; init; }

    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();

    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission, StringComparer.Ordinal);
    }
}
