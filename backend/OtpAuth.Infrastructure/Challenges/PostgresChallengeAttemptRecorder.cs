using Dapper;
using Npgsql;
using OtpAuth.Application.Challenges;

namespace OtpAuth.Infrastructure.Challenges;

public sealed class PostgresChallengeAttemptRecorder : IChallengeAttemptRecorder
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresChallengeAttemptRecorder(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task RecordAsync(ChallengeAttemptRecord attempt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.challenge_attempts (
                id,
                challenge_id,
                attempt_type,
                result,
                created_utc
            ) values (
                @Id,
                @ChallengeId,
                @AttemptType,
                @Result,
                @CreatedUtc
            );
            """,
            new
            {
                Id = Guid.NewGuid(),
                attempt.ChallengeId,
                attempt.AttemptType,
                attempt.Result,
                attempt.CreatedUtc,
            },
            cancellationToken: cancellationToken));
    }
}
