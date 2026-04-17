using Dapper;
using Npgsql;

namespace OtpAuth.Infrastructure.Security;

public sealed class PostgresSecurityAuditStore : ISecurityAuditStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresSecurityAuditStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task AppendAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into auth.security_audit_events (
                id,
                event_type,
                subject_type,
                subject_id,
                summary,
                payload_json,
                severity,
                source,
                created_utc
            )
            values (
                @EventId,
                @EventType,
                @SubjectType,
                @SubjectId,
                @Summary,
                cast(@PayloadJson as jsonb),
                @Severity,
                @Source,
                @CreatedUtc
            );
            """,
            auditEvent,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<SecurityAuditEvent>> ListRecentAsync(
        int limit,
        string? eventTypePrefix,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var results = await connection.QueryAsync<SecurityAuditEvent>(new CommandDefinition(
            """
            select
                id as EventId,
                event_type as EventType,
                subject_type as SubjectType,
                subject_id as SubjectId,
                summary as Summary,
                payload_json::text as PayloadJson,
                severity as Severity,
                source as Source,
                created_utc as CreatedUtc
            from auth.security_audit_events
            where @EventTypePrefix is null
               or event_type like @EventTypePattern
            order by created_utc desc, id desc
            limit @Limit;
            """,
            new
            {
                Limit = limit,
                EventTypePrefix = eventTypePrefix,
                EventTypePattern = string.IsNullOrWhiteSpace(eventTypePrefix)
                    ? null
                    : $"{eventTypePrefix}%",
            },
            cancellationToken: cancellationToken));

        return results.ToArray();
    }
}
