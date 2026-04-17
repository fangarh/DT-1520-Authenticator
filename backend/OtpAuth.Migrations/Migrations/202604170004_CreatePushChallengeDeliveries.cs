using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604170004)]
public sealed class CreatePushChallengeDeliveries : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table auth.push_challenge_deliveries (
                id uuid primary key,
                challenge_id uuid not null references auth.challenges (id) on delete cascade,
                tenant_id uuid not null,
                application_client_id uuid not null,
                external_user_id varchar(256) not null,
                target_device_id uuid not null references auth.devices (id) on delete restrict,
                status varchar(32) not null,
                attempt_count integer not null default 0,
                next_attempt_utc timestamptz not null,
                last_attempt_utc timestamptz null,
                delivered_utc timestamptz null,
                last_error_code varchar(128) null,
                locked_until_utc timestamptz null,
                provider_message_id varchar(256) null,
                created_utc timestamptz not null default timezone('utc', now()),
                updated_utc timestamptz not null default timezone('utc', now()),
                constraint uq_push_challenge_deliveries_challenge unique (challenge_id),
                constraint chk_push_challenge_deliveries_status check (status in ('queued', 'delivered', 'failed'))
            );

            create index ix_push_challenge_deliveries_due
                on auth.push_challenge_deliveries (status, next_attempt_utc, locked_until_utc);

            create index ix_push_challenge_deliveries_target_device
                on auth.push_challenge_deliveries (target_device_id, created_utc desc);
            """);
    }

    public override void Down()
    {
        Execute.Sql("drop table if exists auth.push_challenge_deliveries;");
    }
}
