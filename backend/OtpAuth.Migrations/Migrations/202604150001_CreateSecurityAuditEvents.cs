using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604150001)]
public sealed class CreateSecurityAuditEvents : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table if not exists auth.security_audit_events (
                id uuid primary key,
                event_type varchar(128) not null,
                subject_type varchar(64) not null,
                subject_id varchar(256) null,
                summary varchar(512) not null,
                payload_json jsonb not null,
                severity varchar(32) not null,
                source varchar(64) not null,
                created_utc timestamptz not null
            );

            create index if not exists ix_security_audit_events_created_utc
                on auth.security_audit_events (created_utc desc);

            create index if not exists ix_security_audit_events_event_type_created_utc
                on auth.security_audit_events (event_type, created_utc desc);

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
            select
                id,
                event_type,
                'signing_key',
                current_key_id,
                summary,
                payload_json,
                case
                    when warning_count > 0 then 'warning'
                    else 'info'
                end,
                'specialized_audit_backfill',
                created_utc
            from auth.signing_key_audit_events
            on conflict (id) do nothing;

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
            select
                id,
                event_type,
                'totp_protection_key',
                current_key_version::text,
                summary,
                payload_json,
                case
                    when warning_count > 0 then 'warning'
                    else 'info'
                end,
                'specialized_audit_backfill',
                created_utc
            from auth.totp_protection_key_audit_events
            on conflict (id) do nothing;
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.security_audit_events;
            """);
    }
}
