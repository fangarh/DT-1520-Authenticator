using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604200001)]
public sealed class CreateChallengeCallbackDeliveries : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table auth.challenge_callback_deliveries (
                id uuid primary key,
                challenge_id uuid not null references auth.challenges(id) on delete cascade,
                tenant_id uuid not null,
                application_client_id uuid not null,
                callback_url varchar(2048) not null,
                event_type varchar(64) not null,
                occurred_utc timestamptz not null,
                status varchar(32) not null,
                attempt_count integer not null,
                next_attempt_utc timestamptz not null,
                last_attempt_utc timestamptz null,
                delivered_utc timestamptz null,
                last_error_code varchar(128) null,
                locked_until_utc timestamptz null,
                created_utc timestamptz not null,
                updated_utc timestamptz not null,
                constraint uq_challenge_callback_deliveries_event unique (challenge_id, event_type),
                constraint chk_challenge_callback_deliveries_event_type check (event_type in ('challenge.approved', 'challenge.denied', 'challenge.expired')),
                constraint chk_challenge_callback_deliveries_status check (status in ('queued', 'delivered', 'failed'))
            );

            create index ix_challenge_callback_deliveries_due
                on auth.challenge_callback_deliveries (status, next_attempt_utc, locked_until_utc);
            """);
    }

    public override void Down()
    {
        Execute.Sql("drop table if exists auth.challenge_callback_deliveries;");
    }
}
