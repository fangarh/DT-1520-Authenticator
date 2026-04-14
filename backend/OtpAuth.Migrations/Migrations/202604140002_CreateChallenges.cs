using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604140002)]
public sealed class CreateChallenges : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table if not exists auth.challenges (
                id uuid primary key,
                tenant_id uuid not null,
                application_client_id uuid not null,
                external_user_id varchar(256) not null,
                username varchar(256) null,
                operation_type integer not null,
                operation_display_name varchar(256) null,
                factor_type integer not null,
                status integer not null,
                expires_at timestamptz not null,
                correlation_id varchar(128) null,
                callback_url varchar(2048) null,
                created_utc timestamptz not null default timezone('utc', now()),
                updated_utc timestamptz not null default timezone('utc', now())
            );

            create index if not exists ix_challenges_tenant_application
                on auth.challenges (tenant_id, application_client_id);

            create index if not exists ix_challenges_status_expires_at
                on auth.challenges (status, expires_at);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.challenges;
            """);
    }
}
