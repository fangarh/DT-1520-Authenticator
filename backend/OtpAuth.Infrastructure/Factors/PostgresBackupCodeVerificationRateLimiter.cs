using Dapper;
using Npgsql;
using OtpAuth.Application.Challenges;
using OtpAuth.Application.Factors;
using OtpAuth.Domain.Challenges;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresBackupCodeVerificationRateLimiter : IBackupCodeVerificationRateLimiter
{
    private const int AttemptLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly NpgsqlDataSource _dataSource;

    public PostgresBackupCodeVerificationRateLimiter(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<BackupCodeVerificationRateLimitDecision> EvaluateAsync(
        Challenge challenge,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var windowStart = timestamp.Subtract(Window);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var attempts = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            select count(*)
            from auth.challenge_attempts attempt
            inner join auth.challenges challenge on challenge.id = attempt.challenge_id
            where challenge.tenant_id = @TenantId
              and challenge.application_client_id = @ApplicationClientId
              and challenge.external_user_id = @ExternalUserId
              and attempt.attempt_type = @AttemptType
              and attempt.result = @InvalidCode
              and attempt.created_utc >= @WindowStart;
            """,
            new
            {
                challenge.TenantId,
                challenge.ApplicationClientId,
                challenge.ExternalUserId,
                AttemptType = ChallengeAttemptTypes.BackupCodeVerify,
                InvalidCode = ChallengeAttemptResults.InvalidCode,
                WindowStart = windowStart,
            },
            cancellationToken: cancellationToken));

        return attempts >= AttemptLimit
            ? BackupCodeVerificationRateLimitDecision.Denied((int)Window.TotalSeconds)
            : BackupCodeVerificationRateLimitDecision.Allowed();
    }
}
