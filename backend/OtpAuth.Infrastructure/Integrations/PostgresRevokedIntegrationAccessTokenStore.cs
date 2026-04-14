using Dapper;
using Npgsql;
using OtpAuth.Application.Integrations;

namespace OtpAuth.Infrastructure.Integrations;

public sealed class PostgresRevokedIntegrationAccessTokenStore : IIntegrationAccessTokenRevocationStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresRevokedIntegrationAccessTokenStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> IsRevokedAsync(string jwtId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var revoked = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            select 1
            from auth.revoked_integration_access_tokens
            where jwt_id = @JwtId
            limit 1;
            """,
            new { JwtId = jwtId },
            cancellationToken: cancellationToken));

        return revoked.HasValue;
    }

    public async Task RevokeAsync(
        RevokedIntegrationAccessToken token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.revoked_integration_access_tokens (
                jwt_id,
                client_id,
                revoked_utc,
                expires_utc,
                reason
            ) values (
                @JwtId,
                @ClientId,
                @RevokedAtUtc,
                @ExpiresAtUtc,
                @Reason
            )
            on conflict (jwt_id) do update
            set revoked_utc = excluded.revoked_utc,
                expires_utc = excluded.expires_utc,
                reason = excluded.reason;
            """,
            token,
            cancellationToken: cancellationToken));
    }
}
