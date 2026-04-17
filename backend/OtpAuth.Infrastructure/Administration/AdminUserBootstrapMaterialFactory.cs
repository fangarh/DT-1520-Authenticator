using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

public sealed class AdminUserBootstrapMaterialFactory
{
    private static readonly HashSet<string> AllowedPermissions =
    [
        AdminPermissions.EnrollmentsRead,
        AdminPermissions.EnrollmentsWrite,
    ];

    private readonly IAdminPasswordHasher _passwordHasher;

    public AdminUserBootstrapMaterialFactory(IAdminPasswordHasher passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public AdminUserBootstrapMaterial Create(
        string username,
        string password,
        IEnumerable<string> permissions)
    {
        var normalizedUsername = NormalizeUsername(username);
        var normalizedPassword = ValidatePassword(password);
        var normalizedPermissions = NormalizePermissions(permissions);

        return new AdminUserBootstrapMaterial
        {
            Username = normalizedUsername,
            NormalizedUsername = normalizedUsername.ToUpperInvariant(),
            PasswordHash = _passwordHasher.Hash(normalizedPassword),
            Permissions = normalizedPermissions,
        };
    }

    private static string NormalizeUsername(string username)
    {
        var normalizedUsername = username?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            throw new InvalidOperationException("Admin username is required.");
        }

        if (normalizedUsername.Length > 200)
        {
            throw new InvalidOperationException("Admin username must be 200 characters or fewer.");
        }

        return normalizedUsername;
    }

    private static string ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Environment variable 'OTPAUTH_ADMIN_PASSWORD' is required.");
        }

        var normalizedPassword = password.Trim();
        if (normalizedPassword.Length < 12)
        {
            throw new InvalidOperationException("Admin password must be at least 12 characters long.");
        }

        return normalizedPassword;
    }

    private static IReadOnlyCollection<string> NormalizePermissions(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var normalizedPermissions = permissions
            .Select(static permission => permission?.Trim())
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(static permission => permission!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static permission => permission, StringComparer.Ordinal)
            .ToArray();

        if (normalizedPermissions.Length == 0)
        {
            throw new InvalidOperationException("At least one admin permission must be provided.");
        }

        var unknownPermission = normalizedPermissions.FirstOrDefault(permission => !AllowedPermissions.Contains(permission));
        if (unknownPermission is not null)
        {
            throw new InvalidOperationException($"Unsupported admin permission '{unknownPermission}'.");
        }

        return normalizedPermissions;
    }
}
