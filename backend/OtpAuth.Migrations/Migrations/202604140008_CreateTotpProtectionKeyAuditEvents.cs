using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604140008)]
public sealed class CreateTotpProtectionKeyAuditEvents : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table if not exists auth.totp_protection_key_audit_events (
                id uuid primary key,
                event_type varchar(128) not null,
                current_key_version integer not null,
                enrollments_requiring_reencryption_count integer not null,
                warning_count integer not null,
                summary varchar(512) not null,
                payload_json jsonb not null,
                created_utc timestamptz not null
            );

            create index if not exists ix_totp_protection_key_audit_events_created_utc
                on auth.totp_protection_key_audit_events (created_utc desc);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.totp_protection_key_audit_events;
            """);
    }
}
