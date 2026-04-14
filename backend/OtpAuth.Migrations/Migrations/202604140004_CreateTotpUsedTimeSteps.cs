using FluentMigrator;

namespace OtpAuth.Migrations.Migrations;

[Migration(202604140004)]
public sealed class CreateTotpUsedTimeSteps : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            create table if not exists auth.totp_used_time_steps (
                enrollment_id uuid not null,
                time_step bigint not null,
                used_utc timestamptz not null,
                expires_utc timestamptz not null,
                constraint pk_totp_used_time_steps primary key (enrollment_id, time_step),
                constraint fk_totp_used_time_steps_enrollment_id
                    foreign key (enrollment_id)
                    references auth.totp_enrollments (id)
                    on delete cascade
            );

            create index if not exists ix_totp_used_time_steps_expires_utc
                on auth.totp_used_time_steps (expires_utc);
            """);
    }

    public override void Down()
    {
        Execute.Sql(
            """
            drop table if exists auth.totp_used_time_steps;
            """);
    }
}
