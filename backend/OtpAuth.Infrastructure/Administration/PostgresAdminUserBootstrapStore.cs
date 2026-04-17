using Dapper;
using Npgsql;

namespace OtpAuth.Infrastructure.Administration;

public sealed class PostgresAdminUserBootstrapStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAdminUserBootstrapStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<AdminUserBootstrapSummary>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var users = (await connection.QueryAsync<AdminUserRecord>(new CommandDefinition(
            """
            select
                id as AdminUserId,
                username as Username,
                is_active as IsActive
            from auth.admin_users
            order by username, id;
            """,
            cancellationToken: cancellationToken)))
            .ToArray();

        if (users.Length == 0)
        {
            return [];
        }

        var permissions = (await connection.QueryAsync<AdminUserPermissionRecord>(new CommandDefinition(
            """
            select
                admin_user_id as AdminUserId,
                permission as Permission
            from auth.admin_user_permissions
            order by admin_user_id, permission;
            """,
            cancellationToken: cancellationToken)))
            .GroupBy(static item => item.AdminUserId)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyCollection<string>)group
                    .Select(static item => item.Permission)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static permission => permission, StringComparer.Ordinal)
                    .ToArray());

        return users
            .Select(user => new AdminUserBootstrapSummary
            {
                AdminUserId = user.AdminUserId,
                Username = user.Username,
                IsActive = user.IsActive,
                Permissions = permissions.GetValueOrDefault(user.AdminUserId, Array.Empty<string>()),
            })
            .ToArray();
    }

    public async Task<AdminUserBootstrapSummary> UpsertAsync(
        AdminUserBootstrapMaterial material,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(material);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var adminUserId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            """
            insert into auth.admin_users (
                id,
                username,
                normalized_username,
                password_hash,
                is_active,
                created_utc,
                updated_utc,
                last_login_utc
            )
            values (
                gen_random_uuid(),
                @Username,
                @NormalizedUsername,
                @PasswordHash,
                true,
                timezone('utc', now()),
                timezone('utc', now()),
                null
            )
            on conflict (normalized_username) do update
            set username = excluded.username,
                password_hash = excluded.password_hash,
                is_active = true,
                updated_utc = timezone('utc', now())
            returning id;
            """,
            new
            {
                material.Username,
                material.NormalizedUsername,
                material.PasswordHash,
            },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            delete from auth.admin_user_permissions
            where admin_user_id = @AdminUserId;
            """,
            new { AdminUserId = adminUserId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        foreach (var permission in material.Permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                insert into auth.admin_user_permissions (
                    admin_user_id,
                    permission
                )
                values (
                    @AdminUserId,
                    @Permission
                );
                """,
                new
                {
                    AdminUserId = adminUserId,
                    Permission = permission,
                },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);

        return new AdminUserBootstrapSummary
        {
            AdminUserId = adminUserId,
            Username = material.Username,
            IsActive = true,
            Permissions = material.Permissions,
        };
    }

    private sealed record AdminUserRecord
    {
        public required Guid AdminUserId { get; init; }

        public required string Username { get; init; }

        public required bool IsActive { get; init; }
    }

    private sealed record AdminUserPermissionRecord
    {
        public required Guid AdminUserId { get; init; }

        public required string Permission { get; init; }
    }
}
