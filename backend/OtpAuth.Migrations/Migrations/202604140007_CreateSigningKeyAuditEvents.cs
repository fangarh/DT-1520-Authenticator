using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604140007)]
public sealed class CreateSigningKeyAuditEvents : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table if not exists auth.signing_key_audit_events (
                id uuid primary key,
                event_type varchar(128) not null,
                current_key_id varchar(200) not null,
                active_legacy_key_count integer not null,
                retired_legacy_key_count integer not null,
                warning_count integer not null,
                summary varchar(512) not null,
                payload_json jsonb not null,
                created_utc timestamptz not null
            );

            create index if not exists ix_signing_key_audit_events_created_utc
                on auth.signing_key_audit_events (created_utc desc);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.signing_key_audit_events;
            """);
    }
}
