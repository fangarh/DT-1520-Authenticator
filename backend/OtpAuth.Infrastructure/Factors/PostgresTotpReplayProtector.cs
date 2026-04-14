using Dapper;
using Npgsql;
using OtpAuth.Application.Factors;

namespace OtpAuth.Infrastructure.Factors;

public sealed class PostgresTotpReplayProtector : ITotpReplayProtector
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTotpReplayProtector(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> TryReserveAsync(
        Guid enrollmentId,
        long timeStep,
        DateTimeOffset usedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.totp_used_time_steps (
                enrollment_id,
                time_step,
                used_utc,
                expires_utc
            ) values (
                @EnrollmentId,
                @TimeStep,
                @UsedAt,
                @ExpiresAt
            )
            on conflict (enrollment_id, time_step) do nothing;
            """,
            new
            {
                EnrollmentId = enrollmentId,
                TimeStep = timeStep,
                UsedAt = usedAt,
                ExpiresAt = expiresAt,
            },
            cancellationToken: cancellationToken));

        return rowsAffected == 1;
    }
}
