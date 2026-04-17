using Dapper;
using Npgsql;
using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

public sealed class PostgresAdminUserStore : IAdminUserStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAdminUserStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<AdminUser?> GetByNormalizedUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var userRecord = await connection.QuerySingleOrDefaultAsync<AdminUserRecord>(new CommandDefinition(
            """
            select
                id as AdminUserId,
                username as Username,
                normalized_username as NormalizedUsername,
                password_hash as PasswordHash,
                is_active as IsActive
            from auth.admin_users
            where normalized_username = @NormalizedUsername
            limit 1;
            """,
            new
            {
                NormalizedUsername = normalizedUsername,
            },
            cancellationToken: cancellationToken));

        if (userRecord is null)
        {
            return null;
        }

        var permissions = (await connection.QueryAsync<string>(new CommandDefinition(
            """
            select permission
            from auth.admin_user_permissions
            where admin_user_id = @AdminUserId
            order by permission;
            """,
            new
            {
                userRecord.AdminUserId,
            },
            cancellationToken: cancellationToken)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new AdminUser
        {
            AdminUserId = userRecord.AdminUserId,
            Username = userRecord.Username,
            NormalizedUsername = userRecord.NormalizedUsername,
            PasswordHash = userRecord.PasswordHash,
            IsActive = userRecord.IsActive,
            Permissions = permissions,
        };
    }

    private sealed record AdminUserRecord
    {
        public required Guid AdminUserId { get; init; }

        public required string Username { get; init; }

        public required string NormalizedUsername { get; init; }

        public required string PasswordHash { get; init; }

        public bool IsActive { get; init; }
    }
}
