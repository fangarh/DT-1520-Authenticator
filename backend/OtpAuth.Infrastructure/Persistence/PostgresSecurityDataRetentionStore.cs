using Dapper;
using Npgsql;

namespace OtpAuth.Infrastructure.Persistence;

public sealed class PostgresSecurityDataRetentionStore : ISecurityDataRetentionStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSecurityDataRetentionStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<int> DeleteExpiredTotpUsedTimeStepsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            delete from auth.totp_used_time_steps
            where expires_utc < @UtcNow;
            """,
            new { UtcNow = utcNow },
            cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteExpiredRevokedIntegrationAccessTokensAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            delete from auth.revoked_integration_access_tokens
            where expires_utc < @UtcNow;
            """,
            new { UtcNow = utcNow },
            cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteChallengeAttemptsOlderThanAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            delete from auth.challenge_attempts
            where created_utc < @CutoffUtc;
            """,
            new { CutoffUtc = cutoffUtc },
            cancellationToken: cancellationToken));
    }
}
